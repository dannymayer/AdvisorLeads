using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Downloads and parses historical Form ADV Part 1 CSV data from the SEC FOIA website.
/// Provides 10+ years of filing history including ownership structures (Schedule A/B).
/// Source: https://www.sec.gov/foia-services/frequently-requested-documents/form-adv-data
/// </summary>
public class FormAdvHistoricalService
{
    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(30)
    };

    private const string FoiaPageUrl =
        "https://www.sec.gov/foia-services/frequently-requested-documents/form-adv-data";
    private const string SecBaseUrl = "https://www.sec.gov";
    private const int BatchSize = 5000;

    private readonly string _cacheDirectory;
    private readonly string _dbPath;

    static FormAdvHistoricalService()
    {
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "AdvisorLeads/1.0 (advisor research tool; contact@advisorleads.com)");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.5");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.sec.gov/");
    }

    public FormAdvHistoricalService(string databasePath)
    {
        _dbPath = databasePath;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _cacheDirectory = Path.Combine(appData, "AdvisorLeads", "SecAdvHistoryCache");
        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <summary>
    /// Discovers available Form ADV CSV data file URLs from the SEC FOIA page.
    /// </summary>
    public async Task<List<string>> DiscoverAvailableFilesAsync(CancellationToken ct = default)
    {
        var urls = new List<string>();
        string html;
        try
        {
            html = await _http.GetStringAsync(FoiaPageUrl, ct);
        }
        catch
        {
            return urls;
        }

        // Match ZIP file links that look like Form ADV data files
        var matches = Regex.Matches(html,
            @"href=""(/files/[^""]+\.zip)""",
            RegexOptions.IgnoreCase);

        foreach (Match m in matches)
        {
            var href = m.Groups[1].Value;
            // Only include links that reference ADV filing data
            if (href.Contains("adv", StringComparison.OrdinalIgnoreCase))
                urls.Add(SecBaseUrl + href);
        }

        return urls;
    }

    /// <summary>
    /// Downloads a ZIP file, extracts CSVs, and parses filing + ownership records.
    /// Returns the count of records processed.
    /// </summary>
    public async Task<(int filings, int owners)> ImportHistoricalDataAsync(
        string zipUrl,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var fileName = Path.GetFileName(new Uri(zipUrl).LocalPath);
        var localZipPath = Path.Combine(_cacheDirectory, fileName);

        if (!File.Exists(localZipPath))
        {
            progress?.Report($"Form ADV History: Downloading {fileName}...");
            var bytes = await _http.GetByteArrayAsync(zipUrl, ct);
            await File.WriteAllBytesAsync(localZipPath, bytes, ct);
        }
        else
        {
            progress?.Report($"Form ADV History: Using cached {fileName}...");
        }

        progress?.Report("Form ADV History: Extracting and parsing CSV data...");

        int totalFilings = 0;
        int totalOwners = 0;

        using var archive = ZipFile.OpenRead(localZipPath);

        // Parse Registration/filing CSVs
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var entryLower = entry.Name.ToLowerInvariant();

            if (entryLower.Contains("registration") && entryLower.EndsWith(".csv"))
            {
                progress?.Report($"Form ADV History: Parsing {entry.Name}...");
                var filings = ParseFilingsCsv(entry);
                totalFilings += await SaveFilingsAsync(filings, progress, ct);
            }
            else if (entryLower.Contains("schedule_a") && entryLower.EndsWith(".csv"))
            {
                progress?.Report($"Form ADV History: Parsing {entry.Name} (direct owners)...");
                var owners = ParseOwnershipCsv(entry, isDirectOwner: true);
                totalOwners += await SaveOwnersAsync(owners, progress, ct);
            }
            else if (entryLower.Contains("schedule_b") && entryLower.EndsWith(".csv"))
            {
                progress?.Report($"Form ADV History: Parsing {entry.Name} (indirect owners)...");
                var owners = ParseOwnershipCsv(entry, isDirectOwner: false);
                totalOwners += await SaveOwnersAsync(owners, progress, ct);
            }
        }

        progress?.Report($"Form ADV History: Imported {totalFilings:N0} filings and {totalOwners:N0} ownership records.");
        return (totalFilings, totalOwners);
    }

    /// <summary>
    /// Gets the most recent filing for a firm by CRD number.
    /// </summary>
    public FormAdvFiling? GetLatestFiling(string firmCrd)
    {
        using var db = new DatabaseContext(_dbPath);
        return db.FormAdvFilings
            .Where(f => f.FirmCrd == firmCrd)
            .OrderByDescending(f => f.FilingDate)
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets all filings for a firm ordered by date descending.
    /// </summary>
    public List<FormAdvFiling> GetFilingHistory(string firmCrd)
    {
        using var db = new DatabaseContext(_dbPath);
        return db.FormAdvFilings
            .Where(f => f.FirmCrd == firmCrd)
            .OrderByDescending(f => f.FilingDate)
            .ToList();
    }

    /// <summary>
    /// Gets current ownership records for a firm.
    /// </summary>
    public List<FirmOwnership> GetFirmOwnership(string firmCrd)
    {
        using var db = new DatabaseContext(_dbPath);
        return db.FirmOwnership
            .Where(o => o.FirmCrd == firmCrd)
            .OrderByDescending(o => o.FilingDate)
            .ThenBy(o => o.OwnerName)
            .ToList();
    }

    /// <summary>
    /// Gets ownership records from a specific filing date for diff comparison.
    /// </summary>
    public List<FirmOwnership> GetFirmOwnershipAtDate(string firmCrd, DateTime filingDate)
    {
        using var db = new DatabaseContext(_dbPath);
        return db.FirmOwnership
            .Where(o => o.FirmCrd == firmCrd && o.FilingDate == filingDate)
            .OrderBy(o => o.OwnerName)
            .ToList();
    }

    // ── Private helpers ─────────────────────────────────────────────

    private List<FormAdvFiling> ParseFilingsCsv(ZipArchiveEntry entry)
    {
        var filings = new List<FormAdvFiling>();
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var headerLine = reader.ReadLine();
        if (headerLine == null) return filings;

        var headers = ParseCsvLine(headerLine);
        var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
            colIndex[headers[i].Trim()] = i;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ParseCsvLine(line);
            var filing = MapRowToFiling(cols, colIndex);
            if (filing != null) filings.Add(filing);
        }

        return filings;
    }

    private List<FirmOwnership> ParseOwnershipCsv(ZipArchiveEntry entry, bool isDirectOwner)
    {
        var owners = new List<FirmOwnership>();
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var headerLine = reader.ReadLine();
        if (headerLine == null) return owners;

        var headers = ParseCsvLine(headerLine);
        var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
            colIndex[headers[i].Trim()] = i;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ParseCsvLine(line);
            var owner = MapRowToOwnership(cols, colIndex, isDirectOwner);
            if (owner != null) owners.Add(owner);
        }

        return owners;
    }

    private static FormAdvFiling? MapRowToFiling(List<string> cols, Dictionary<string, int> idx)
    {
        string Get(string key)
            => idx.TryGetValue(key, out var i) && i < cols.Count ? cols[i].Trim() : string.Empty;

        var crd = Get("Organization CRD#");
        if (string.IsNullOrWhiteSpace(crd))
            crd = Get("CRD Number");
        if (string.IsNullOrWhiteSpace(crd))
            crd = Get("CRDNumber");
        if (string.IsNullOrWhiteSpace(crd)) return null;

        DateTime? filingDate = null;
        foreach (var key in new[] { "Filing Date", "FilingDate", "Latest ADV Filing Date", "Date" })
        {
            var raw = Get(key);
            if (!string.IsNullOrWhiteSpace(raw) && DateTime.TryParse(raw, out var d))
            {
                filingDate = d;
                break;
            }
        }
        if (!filingDate.HasValue) return null;

        // AUM columns (try multiple known column names)
        decimal? regAum = ParseDecimal(Get("5F(2)(a)"), Get("Regulatory AUM - Discretionary"),
            Get("RegulatoryAUM"), Get("5F2a"));
        decimal? regAumNd = ParseDecimal(Get("5F(2)(b)"), Get("Regulatory AUM - Non-Discretionary"),
            Get("5F2b"));

        decimal? totalAum = null;
        if (regAum.HasValue || regAumNd.HasValue)
            totalAum = (regAum ?? 0) + (regAumNd ?? 0);
        var totalAumRaw = ParseDecimal(Get("5F(2)(c)"), Get("Total Regulatory AUM"), Get("5F2c"));
        if (totalAumRaw.HasValue) totalAum = totalAumRaw;

        int? numEmployees = ParseInt(Get("5A"), Get("Number of Employees"), Get("NumEmployees"));
        int? numAdvisors = ParseInt(Get("5B(1)"), Get("Number of IARs"), Get("5B1"));
        int? numClients = ParseInt(Get("5D"), Get("5D(a)"), Get("5D1"), Get("Number of Clients"));

        var filingType = Get("Filing Type");
        if (string.IsNullOrWhiteSpace(filingType))
            filingType = Get("ADV Filing Type");
        if (string.IsNullOrWhiteSpace(filingType))
            filingType = null;

        var regStatus = Get("SEC Current Status");
        if (string.IsNullOrWhiteSpace(regStatus))
            regStatus = Get("Registration Status");
        if (string.IsNullOrWhiteSpace(regStatus))
            regStatus = null;

        var bizName = Get("Primary Business Name");
        if (string.IsNullOrWhiteSpace(bizName))
            bizName = Get("Business Name");
        if (string.IsNullOrWhiteSpace(bizName))
            bizName = null;

        return new FormAdvFiling
        {
            FirmCrd = crd,
            FilingDate = filingDate.Value,
            FilingType = filingType,
            RegulatoryAum = regAum,
            RegulatoryAumNonDiscretionary = regAumNd,
            TotalAum = totalAum,
            NumberOfEmployees = numEmployees,
            NumberOfAdvisors = numAdvisors,
            NumClients = numClients,
            RegistrationStatus = regStatus,
            BusinessName = bizName,
        };
    }

    private static FirmOwnership? MapRowToOwnership(List<string> cols, Dictionary<string, int> idx, bool isDirectOwner)
    {
        string Get(string key)
            => idx.TryGetValue(key, out var i) && i < cols.Count ? cols[i].Trim() : string.Empty;

        var firmCrd = Get("Organization CRD#");
        if (string.IsNullOrWhiteSpace(firmCrd))
            firmCrd = Get("CRD Number");
        if (string.IsNullOrWhiteSpace(firmCrd))
            firmCrd = Get("CRDNumber");
        if (string.IsNullOrWhiteSpace(firmCrd)) return null;

        var ownerName = Get("Name");
        if (string.IsNullOrWhiteSpace(ownerName))
            ownerName = Get("Owner Name");
        if (string.IsNullOrWhiteSpace(ownerName))
            ownerName = Get("Full Legal Name");
        if (string.IsNullOrWhiteSpace(ownerName)) return null;

        DateTime? filingDate = null;
        foreach (var key in new[] { "Filing Date", "FilingDate", "Latest ADV Filing Date", "Date" })
        {
            var raw = Get(key);
            if (!string.IsNullOrWhiteSpace(raw) && DateTime.TryParse(raw, out var d))
            {
                filingDate = d;
                break;
            }
        }
        if (!filingDate.HasValue) return null;

        // Ownership percentage
        decimal? pct = null;
        var pctRaw = Get("Ownership %");
        if (string.IsNullOrWhiteSpace(pctRaw)) pctRaw = Get("Ownership Percentage");
        if (string.IsNullOrWhiteSpace(pctRaw)) pctRaw = Get("% of Ownership Interest");
        if (string.IsNullOrWhiteSpace(pctRaw)) pctRaw = Get("Ownership Code");
        if (!string.IsNullOrWhiteSpace(pctRaw))
        {
            // Handle percentage codes like "A = less than 5%", "B = 5-10%", etc.
            pct = pctRaw.Trim().ToUpperInvariant() switch
            {
                "A" => 2.5m,   // Less than 5%
                "B" => 7.5m,   // 5% but less than 10%
                "C" => 17.5m,  // 10% but less than 25%
                "D" => 37.5m,  // 25% but less than 50%
                "E" => 62.5m,  // 50% but less than 75%
                "F" => 75m,    // 75% or more
                _ => decimal.TryParse(pctRaw.Replace("%", "").Replace(",", ""),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : null
            };
        }

        var title = NullIfEmpty(Get("Title or Status"));
        if (title == null) title = NullIfEmpty(Get("Title"));

        var entityType = NullIfEmpty(Get("Entity Type"));
        if (entityType == null) entityType = NullIfEmpty(Get("Type"));
        if (entityType == null) entityType = NullIfEmpty(Get("Entity In Which Interest is Owned"));

        var status = NullIfEmpty(Get("Status"));
        if (status == null) status = NullIfEmpty(Get("Control Person Status"));

        var ownerCrd = NullIfEmpty(Get("Owner CRD#"));
        if (ownerCrd == null) ownerCrd = NullIfEmpty(Get("CRD# of Owner"));
        if (ownerCrd == null) ownerCrd = NullIfEmpty(Get("Owner CRD Number"));

        return new FirmOwnership
        {
            FirmCrd = firmCrd,
            FilingDate = filingDate.Value,
            OwnerName = ownerName,
            Title = title,
            OwnershipPercent = pct,
            IsDirectOwner = isDirectOwner,
            EntityType = entityType,
            Status = status,
            OwnerCrd = ownerCrd,
        };
    }

    private async Task<int> SaveFilingsAsync(
        List<FormAdvFiling> filings,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        if (filings.Count == 0) return 0;

        int saved = 0;
        using var db = new DatabaseContext(_dbPath);

        for (int i = 0; i < filings.Count; i += BatchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = filings.GetRange(i, Math.Min(BatchSize, filings.Count - i));
            db.FormAdvFilings.AddRange(batch);
            await db.SaveChangesAsync(ct);
            saved += batch.Count;
            progress?.Report($"Form ADV History: Saved {saved:N0} / {filings.Count:N0} filings...");
        }

        return saved;
    }

    private async Task<int> SaveOwnersAsync(
        List<FirmOwnership> owners,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        if (owners.Count == 0) return 0;

        int saved = 0;
        using var db = new DatabaseContext(_dbPath);

        for (int i = 0; i < owners.Count; i += BatchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = owners.GetRange(i, Math.Min(BatchSize, owners.Count - i));
            db.FirmOwnership.AddRange(batch);
            await db.SaveChangesAsync(ct);
            saved += batch.Count;
            progress?.Report($"Form ADV History: Saved {saved:N0} / {owners.Count:N0} ownership records...");
        }

        return saved;
    }

    // ── Parsing utilities ───────────────────────────────────────────

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s;

    private static int? ParseInt(params string[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v) &&
                int.TryParse(v.Replace(",", ""), out var n))
                return n;
        }
        return null;
    }

    private static decimal? ParseDecimal(params string[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v) &&
                decimal.TryParse(v.Replace(",", ""),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d))
                return d;
        }
        return null;
    }

    /// <summary>RFC 4180-compliant CSV line parser.</summary>
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    { current.Append('"'); i++; }
                    else inQuotes = false;
                }
                else current.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields;
    }
}
