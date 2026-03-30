using System.Net.Http.Headers;
using AdvisorLeads.Data;
using AdvisorLeads.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace AdvisorLeads.Services;

/// <summary>
/// Integrates with the SEC EDGAR Submissions API to fetch filing history
/// for investment adviser firms. Tracks Form ADV filings and amendments.
/// API docs: https://www.sec.gov/search-filings/edgar-application-programming-interfaces
/// Rate limit: 10 requests/second, must include User-Agent with email.
/// </summary>
public class EdgarSubmissionsService
{
    private static readonly HttpClient _http = new HttpClient();
    private const string BaseUrl = "https://data.sec.gov/submissions/CIK";
    private const string CompanySearchUrl = "https://efts.sec.gov/LATEST/search-index";
    private const int RateLimitDelayMs = 150; // stay well under 10/sec
    private readonly string _dbPath;
    private readonly Dictionary<string, string> _cikCache = new();

    private static readonly HashSet<string> AdvFormTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADV", "ADV/A", "ADV-W", "ADV-E", "ADV-H", "ADV-NR"
    };

    static EdgarSubmissionsService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "AdvisorLeads/1.0 (advisor-research; contact@advisorleads.com)");
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public EdgarSubmissionsService(string databasePath)
    {
        _dbPath = databasePath;
    }

    /// <summary>
    /// Fetches EDGAR filing history for a firm using its SEC number.
    /// Returns the number of new filings stored.
    /// </summary>
    public async Task<int> FetchFilingsForFirmAsync(
        string firmCrd, string? secNumber, string? cik, string? firmName = null,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cik))
        {
            if (string.IsNullOrWhiteSpace(secNumber))
            {
                progress?.Report($"Firm {firmCrd}: no SEC number or CIK available, skipping.");
                return 0;
            }

            cik = await LookupCikAsync(secNumber, firmName, ct);
            if (string.IsNullOrWhiteSpace(cik))
            {
                progress?.Report($"Firm {firmCrd}: could not resolve CIK for SEC# {secNumber}.");
                return 0;
            }
        }

        var paddedCik = cik.PadLeft(10, '0');
        var url = $"{BaseUrl}{paddedCik}.json";

        progress?.Report($"Firm {firmCrd}: fetching EDGAR submissions (CIK {cik})...");

        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(url, ct);
        }
        catch (Exception ex)
        {
            progress?.Report($"Firm {firmCrd}: HTTP error — {ex.Message}");
            return 0;
        }

        if (!response.IsSuccessStatusCode)
        {
            progress?.Report($"Firm {firmCrd}: EDGAR returned {(int)response.StatusCode} for CIK {cik}.");
            return 0;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var root = JObject.Parse(json);
        var recent = root.SelectToken("filings.recent");

        if (recent == null)
        {
            progress?.Report($"Firm {firmCrd}: no recent filings in EDGAR response.");
            return 0;
        }

        var accessionNumbers = recent["accessionNumber"]?.ToObject<string[]>() ?? [];
        var filingDates = recent["filingDate"]?.ToObject<string[]>() ?? [];
        var forms = recent["form"]?.ToObject<string[]>() ?? [];
        var primaryDocs = recent["primaryDocument"]?.ToObject<string[]>() ?? [];
        var descriptions = recent["primaryDocDescription"]?.ToObject<string[]>() ?? [];
        var acceptanceDates = recent["acceptanceDatetime"]?.ToObject<string[]>() ?? [];
        var sizes = recent["size"]?.ToObject<long?[]>() ?? [];

        var newFilings = new List<FirmFiling>();

        using (var ctx = new DatabaseContext(_dbPath))
        {
            var firmExists = await ctx.Firms.AsNoTracking()
                .AnyAsync(f => f.CrdNumber == firmCrd, ct);
            if (!firmExists)
            {
                progress?.Report($"Firm {firmCrd}: not in local database, skipping EDGAR filings.");
                return 0;
            }

            var existingAccessions = ctx.FirmFilings
                .AsNoTracking()
                .Where(f => f.FirmCrd == firmCrd)
                .Select(f => f.AccessionNumber)
                .ToHashSet();

            for (int i = 0; i < accessionNumbers.Length; i++)
            {
                var formType = i < forms.Length ? forms[i] : "";
                if (!AdvFormTypes.Contains(formType))
                    continue;

                var accession = accessionNumbers[i];
                if (existingAccessions.Contains(accession))
                    continue;

                var filing = new FirmFiling
                {
                    FirmCrd = firmCrd,
                    Cik = cik,
                    AccessionNumber = accession,
                    FormType = formType,
                    FilingDate = DateTime.TryParse(
                        i < filingDates.Length ? filingDates[i] : null,
                        out var fd) ? fd : DateTime.MinValue,
                    PrimaryDocument = i < primaryDocs.Length ? primaryDocs[i] : null,
                    Description = i < descriptions.Length ? descriptions[i] : null,
                    FileSize = i < sizes.Length ? sizes[i] : null,
                };

                if (i < acceptanceDates.Length
                    && DateTime.TryParse(acceptanceDates[i], out var ad))
                {
                    filing.AcceptanceDate = ad;
                }

                // Build the EDGAR filing URL
                var accessionNoDashes = accession.Replace("-", "");
                if (!string.IsNullOrEmpty(filing.PrimaryDocument))
                {
                    filing.FilingUrl =
                        $"https://www.sec.gov/Archives/edgar/data/{cik}/{accessionNoDashes}/{filing.PrimaryDocument}";
                }

                newFilings.Add(filing);
            }

            if (newFilings.Count > 0)
            {
                ctx.FirmFilings.AddRange(newFilings);
                await ctx.SaveChangesAsync(ct);
            }
        }

        progress?.Report($"Firm {firmCrd}: stored {newFilings.Count} new EDGAR filings.");
        return newFilings.Count;
    }

    /// <summary>
    /// Batch-fetches filings for multiple firms. Processes firms that have SEC numbers
    /// but no filing history stored yet. Respects rate limits.
    /// </summary>
    public async Task<int> FetchFilingsBatchAsync(
        int maxFirms = 100,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        List<Firm> firms;
        using (var ctx = new DatabaseContext(_dbPath))
        {
            // Firms with SEC numbers that have no filings yet
            var firmsWithFilings = ctx.FirmFilings.Select(f => f.FirmCrd).Distinct();
            firms = await ctx.Firms
                .AsNoTracking()
                .Where(f => f.SECNumber != null && f.SECNumber != "")
                .Where(f => !firmsWithFilings.Contains(f.CrdNumber))
                .OrderBy(f => f.Name)
                .Take(maxFirms)
                .ToListAsync(ct);
        }

        progress?.Report($"Found {firms.Count} firms with SEC numbers and no filing history.");

        int totalNew = 0;
        for (int i = 0; i < firms.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var firm = firms[i];
            progress?.Report($"[{i + 1}/{firms.Count}] Processing {firm.Name} (CRD {firm.CrdNumber})...");

            var count = await FetchFilingsForFirmAsync(
                firm.CrdNumber, firm.SECNumber, cik: firm.EdgarCik, firmName: firm.Name, progress, ct);
            totalNew += count;

            if (i < firms.Count - 1)
                await Task.Delay(RateLimitDelayMs, ct);
        }

        progress?.Report($"Batch complete: {totalNew} new filings across {firms.Count} firms.");
        return totalNew;
    }

    /// <summary>
    /// Gets all stored filings for a firm, ordered by filing date descending.
    /// </summary>
    public List<FirmFiling> GetFilings(string firmCrd)
    {
        using var ctx = new DatabaseContext(_dbPath);
        return ctx.FirmFilings
            .AsNoTracking()
            .Where(f => f.FirmCrd == firmCrd)
            .OrderByDescending(f => f.FilingDate)
            .ToList();
    }

    /// <summary>
    /// Gets ADV-specific filings (ADV, ADV/A, ADV-W, ADV-E, ADV-H, ADV-NR) for a firm.
    /// </summary>
    public List<FirmFiling> GetAdvFilings(string firmCrd)
    {
        using var ctx = new DatabaseContext(_dbPath);
        return ctx.FirmFilings
            .AsNoTracking()
            .Where(f => f.FirmCrd == firmCrd && AdvFormTypes.Contains(f.FormType))
            .OrderByDescending(f => f.FilingDate)
            .ToList();
    }

    /// <summary>
    /// Calculates filing frequency metrics for a firm.
    /// Returns (totalFilings, advFilings, amendmentsPerYear, lastFilingDate).
    /// </summary>
    public (int total, int advCount, double amendmentsPerYear, DateTime? lastFiling) GetFilingMetrics(
        string firmCrd)
    {
        using var ctx = new DatabaseContext(_dbPath);
        var filings = ctx.FirmFilings
            .AsNoTracking()
            .Where(f => f.FirmCrd == firmCrd)
            .ToList();

        if (filings.Count == 0)
            return (0, 0, 0, null);

        var advFilings = filings.Where(f => AdvFormTypes.Contains(f.FormType)).ToList();
        var lastFiling = filings.Max(f => f.FilingDate);

        double amendmentsPerYear = 0;
        if (advFilings.Count > 1)
        {
            var earliest = advFilings.Min(f => f.FilingDate);
            var latest = advFilings.Max(f => f.FilingDate);
            var years = (latest - earliest).TotalDays / 365.25;
            if (years > 0)
                amendmentsPerYear = advFilings.Count / years;
        }

        return (filings.Count, advFilings.Count, Math.Round(amendmentsPerYear, 2), lastFiling);
    }

    /// <summary>
    /// Looks up a CIK number from an SEC registration number.
    /// SEC numbers like "801-12345" map to CIK numbers in EDGAR.
    /// Tries four approaches: date-limited EFTS search, browse-edgar filenum,
    /// broad EFTS search, and company name search.
    /// </summary>
    private async Task<string?> LookupCikAsync(string secNumber, string? firmName, CancellationToken ct)
    {
        if (_cikCache.TryGetValue(secNumber, out var cached))
            return cached;

        // Try EDGAR full-text search index (date-limited to recent filings)
        try
        {
            var searchUrl = $"{CompanySearchUrl}?q=%22{Uri.EscapeDataString(secNumber)}%22&dateRange=custom&startdt=2020-01-01&forms=ADV";
            var response = await _http.GetAsync(searchUrl, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var result = JObject.Parse(json);
                var hits = result.SelectToken("hits.hits");
                if (hits is JArray arr && arr.Count > 0)
                {
                    var raw = arr[0].SelectToken("_source.entity_id")?.ToString()
                           ?? arr[0].SelectToken("_source.filer_id")?.ToString();
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        var cikValue = raw.TrimStart('0');
                        if (cikValue.Length == 0) cikValue = "0";
                        _cikCache[secNumber] = cikValue;
                        return cikValue;
                    }
                }
            }
        }
        catch
        {
            // Fall through to next lookup
        }

        await Task.Delay(RateLimitDelayMs, ct);

        // Fallback: browse-edgar using SEC file/registration number (filenum= parameter)
        try
        {
            var browseUrl =
                $"https://www.sec.gov/cgi-bin/browse-edgar?action=getcompany&filenum={Uri.EscapeDataString(secNumber)}&type=ADV&dateb=&owner=include&count=10";
            var response = await _http.GetAsync(browseUrl, ct);
            if (response.IsSuccessStatusCode)
            {
                var html = await response.Content.ReadAsStringAsync(ct);
                var cikMatch = System.Text.RegularExpressions.Regex.Match(
                    html, @"CIK=(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (cikMatch.Success)
                {
                    var cikValue = cikMatch.Groups[1].Value.TrimStart('0');
                    if (cikValue.Length == 0) cikValue = "0";
                    _cikCache[secNumber] = cikValue;
                    return cikValue;
                }
            }
        }
        catch
        {
            // Fall through to next lookup
        }

        await Task.Delay(RateLimitDelayMs, ct);

        // Third fallback: broad EFTS search without date restriction
        try
        {
            var searchUrl = $"{CompanySearchUrl}?q=%22{Uri.EscapeDataString(secNumber)}%22&forms=ADV&hits.hits.total.value=true";
            var response = await _http.GetAsync(searchUrl, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var result = JObject.Parse(json);
                var hits = result.SelectToken("hits.hits");
                if (hits is JArray arr && arr.Count > 0)
                {
                    var raw = arr[0].SelectToken("_source.entity_id")?.ToString()
                           ?? arr[0].SelectToken("_source.filer_id")?.ToString();
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        var cikValue = raw.TrimStart('0');
                        if (cikValue.Length == 0) cikValue = "0";
                        _cikCache[secNumber] = cikValue;
                        return cikValue;
                    }
                }
            }
        }
        catch
        {
            // Fall through to name-based lookup
        }

        // Fourth fallback: company name search (last resort)
        if (!string.IsNullOrWhiteSpace(firmName))
        {
            await Task.Delay(RateLimitDelayMs, ct);
            try
            {
                var searchUrl = $"{CompanySearchUrl}?q=%22{Uri.EscapeDataString(firmName)}%22&forms=ADV";
                var response = await _http.GetAsync(searchUrl, ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    var result = JObject.Parse(json);
                    var hits = result.SelectToken("hits.hits");
                    if (hits is JArray arr && arr.Count > 0)
                    {
                        var raw = arr[0].SelectToken("_source.entity_id")?.ToString()
                               ?? arr[0].SelectToken("_source.filer_id")?.ToString();
                        if (!string.IsNullOrWhiteSpace(raw))
                        {
                            var cikValue = raw.TrimStart('0');
                            if (cikValue.Length == 0) cikValue = "0";
                            _cikCache[secNumber] = cikValue;
                            return cikValue;
                        }
                    }
                }
            }
            catch
            {
                // All lookups failed
            }
        }

        return null;
    }
}
