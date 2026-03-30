using System.IO.Compression;
using System.Text.Json;
using AdvisorLeads.Data;
using Microsoft.EntityFrameworkCore;

namespace AdvisorLeads.Services;

/// <summary>
/// Enriches firm records with EDGAR metadata by streaming the SEC bulk submissions.zip.
/// Unlike EdgarSubmissionsService (which does per-CIK filing-history calls), this service
/// processes the full ~700k-entry ZIP once per day and extracts SIC codes, fiscal year ends,
/// and CIK numbers for all RIA/broker-dealer firms already in our database.
///
/// Matching strategy: SEC file number ("801-XXXXX") from filings.recent.fileNumber is matched
/// against Firm.SECNumber. Falls back to normalized company name as a secondary signal.
///
/// Source: https://www.sec.gov/Archives/edgar/daily-index/bulkdata/submissions.zip
/// Rate limit: no per-request limit; one large download per day.
/// </summary>
public class SecBulkSubmissionsService
{
    private static readonly HttpClient _http = new HttpClient
    {
        // 30-minute timeout to accommodate the ~3-4 GB download on slow connections
        Timeout = TimeSpan.FromMinutes(30)
    };

    private const string SubmissionsZipUrl =
        "https://www.sec.gov/Archives/edgar/daily-index/bulkdata/submissions.zip";
    private const int CacheValidityHours = 24;

    // SIC codes that indicate investment advisory or brokerage activity
    private static readonly HashSet<string> RelevantSicCodes = new(StringComparer.Ordinal)
    {
        "6282", // Investment Advice
        "6211", // Security Brokers, Dealers, and Flotation Companies
        "6199", // Finance Services
        "6726", // Investment Offices, NEC
    };

    private readonly string _cacheDirectory;
    private readonly string _dbPath;

    static SecBulkSubmissionsService()
    {
        _http.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent", "AdvisorLeads/1.0 (advisor research tool; contact@advisorleads.com)");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
    }

    public SecBulkSubmissionsService(string databasePath, string? cacheDirectory = null)
    {
        _dbPath = databasePath;
        if (cacheDirectory != null)
        {
            _cacheDirectory = cacheDirectory;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _cacheDirectory = Path.Combine(appData, "AdvisorLeads", "SecCache");
        }
        Directory.CreateDirectory(_cacheDirectory);
    }

    private string LocalZipPath => Path.Combine(_cacheDirectory, "submissions.zip");

    /// <summary>
    /// Returns true if the local submissions.zip was written within the last 24 hours.
    /// </summary>
    public bool IsCacheValid()
    {
        if (!File.Exists(LocalZipPath)) return false;
        return (DateTime.UtcNow - File.GetLastWriteTimeUtc(LocalZipPath)).TotalHours < CacheValidityHours;
    }

    /// <summary>
    /// Downloads submissions.zip if the cache is stale, then streams each CIK JSON entry
    /// and upserts EDGAR metadata (SIC code, fiscal year end, CIK) for firms already in the DB.
    /// Returns the number of firm records updated.
    /// </summary>
    public async Task<int> SyncFirmMetadataAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!IsCacheValid())
        {
            progress?.Report("SEC Bulk Submissions: Downloading submissions.zip (~3–4 GB, this may take several minutes)...");
            await DownloadZipAsync(progress, ct);
        }
        else
        {
            progress?.Report("SEC Bulk Submissions: Using cached submissions.zip.");
        }

        return await ProcessZipAsync(progress, ct);
    }

    private async Task DownloadZipAsync(IProgress<string>? progress, CancellationToken ct)
    {
        // Stream directly to disk to avoid loading 3-4 GB into memory
        using var response = await _http.GetAsync(
            SubmissionsZipUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var fs = new FileStream(
            LocalZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await response.Content.CopyToAsync(fs, ct);
        progress?.Report("SEC Bulk Submissions: Download complete.");
    }

    private async Task<int> ProcessZipAsync(IProgress<string>? progress, CancellationToken ct)
    {
        // Build lookup of SEC-registered firms: normalized SEC file number -> CRD
        Dictionary<string, string> secNumberToCrd;
        using (var ctx = new DatabaseContext(_dbPath))
        {
            var firms = await ctx.Firms
                .AsNoTracking()
                .Where(f => f.SECNumber != null && f.SECNumber != "")
                .Select(f => new { f.CrdNumber, f.SECNumber })
                .ToListAsync(ct);

            secNumberToCrd = firms.ToDictionary(
                f => NormalizeSec(f.SECNumber!),
                f => f.CrdNumber,
                StringComparer.OrdinalIgnoreCase);
        }

        progress?.Report($"SEC Bulk Submissions: Loaded {secNumberToCrd.Count} SEC-registered firms for matching.");

        var updates = new List<EdgarFirmUpdate>();
        int entriesScanned = 0;

        // ZipFile.OpenRead then iterate entries — never extracts to disk
        using (var archive = ZipFile.OpenRead(LocalZipPath))
        {
            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();

                if (!entry.Name.StartsWith("CIK", StringComparison.OrdinalIgnoreCase) ||
                    !entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                entriesScanned++;
                if (entriesScanned % 50_000 == 0)
                    progress?.Report(
                        $"SEC Bulk Submissions: Scanned {entriesScanned:N0} entries, {updates.Count} matches...");

                using var stream = entry.Open();
                var update = ParseCikEntry(stream, secNumberToCrd);
                if (update is not null)
                    updates.Add(update);
            }
        }

        progress?.Report(
            $"SEC Bulk Submissions: Matched {updates.Count} firms from {entriesScanned:N0} entries. Writing...");

        if (updates.Count > 0)
            ApplyUpdates(updates, progress);

        progress?.Report($"SEC Bulk Submissions: Complete — {updates.Count} firm records enriched.");
        return updates.Count;
    }

    /// <summary>
    /// Parses one CIK JSON entry and returns an update if the entry matches a known firm.
    /// Public to allow direct unit testing of parsing logic without file system access.
    /// </summary>
    public static EdgarFirmUpdate? ParseCikEntry(
        Stream jsonStream,
        Dictionary<string, string> secNumberToCrd)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(jsonStream);
        }
        catch
        {
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (!root.TryGetProperty("cik", out var cikProp)) return null;
            var cik = cikProp.GetString();
            if (string.IsNullOrEmpty(cik)) return null;

            var sic = root.TryGetProperty("sic", out var sicProp)
                ? sicProp.GetString() : null;

            // Discard non-financial entities immediately to keep memory pressure low
            if (sic is not null && !RelevantSicCodes.Contains(sic))
                return null;

            var sicDesc = root.TryGetProperty("sicDescription", out var sdProp)
                ? sdProp.GetString() : null;
            var fiscalYearEnd = root.TryGetProperty("fiscalYearEnd", out var fyProp)
                ? fyProp.GetString() : null;
            var stateOfIncorp = root.TryGetProperty("stateOfIncorporation", out var siProp)
                ? siProp.GetString() : null;

            // Match against our DB via SEC file numbers in the filings array
            string? matchedCrd = null;
            if (root.TryGetProperty("filings", out var filings) &&
                filings.TryGetProperty("recent", out var recent) &&
                recent.TryGetProperty("fileNumber", out var fileNumbers) &&
                fileNumbers.ValueKind == JsonValueKind.Array)
            {
                foreach (var fn in fileNumbers.EnumerateArray())
                {
                    var fileNum = fn.GetString();
                    // "801-XXXXX" is the SEC registration format for investment advisers
                    if (string.IsNullOrEmpty(fileNum) || !fileNum.StartsWith("801-"))
                        continue;

                    if (secNumberToCrd.TryGetValue(NormalizeSec(fileNum), out var crd))
                    {
                        matchedCrd = crd;
                        break;
                    }
                }
            }

            if (matchedCrd is null) return null;

            return new EdgarFirmUpdate(
                Crd: matchedCrd,
                // Strip EDGAR's leading zeros so the stored value matches what the per-CIK API returns
                Cik: cik.TrimStart('0'),
                SicCode: sic,
                SicDescription: sicDesc,
                FiscalYearEnd: fiscalYearEnd,
                StateOfIncorporation: NullIfEmpty(stateOfIncorp));
        }
    }

    private void ApplyUpdates(List<EdgarFirmUpdate> updates, IProgress<string>? progress)
    {
        using var ctx = new DatabaseContext(_dbPath);
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();

        using var txn = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = txn;
        cmd.CommandText = @"
            UPDATE Firms SET
                EdgarCik            = @cik,
                SicCode             = @sic,
                SicDescription      = @sicDesc,
                FiscalYearEnd       = coalesce(@fiscalYearEnd, FiscalYearEnd),
                StateOfOrganization = coalesce(@stateOfIncorp, StateOfOrganization),
                UpdatedAt           = datetime('now')
            WHERE CrdNumber = @crd";

        var pCrd     = cmd.CreateParameter(); pCrd.ParameterName     = "@crd";          cmd.Parameters.Add(pCrd);
        var pCik     = cmd.CreateParameter(); pCik.ParameterName     = "@cik";          cmd.Parameters.Add(pCik);
        var pSic     = cmd.CreateParameter(); pSic.ParameterName     = "@sic";          cmd.Parameters.Add(pSic);
        var pSicDesc = cmd.CreateParameter(); pSicDesc.ParameterName = "@sicDesc";      cmd.Parameters.Add(pSicDesc);
        var pFye     = cmd.CreateParameter(); pFye.ParameterName     = "@fiscalYearEnd";cmd.Parameters.Add(pFye);
        var pState   = cmd.CreateParameter(); pState.ParameterName   = "@stateOfIncorp";cmd.Parameters.Add(pState);

        int written = 0;
        foreach (var u in updates)
        {
            pCrd.Value     = u.Crd;
            pCik.Value     = u.Cik;
            pSic.Value     = (object?)u.SicCode          ?? DBNull.Value;
            pSicDesc.Value = (object?)u.SicDescription   ?? DBNull.Value;
            pFye.Value     = (object?)u.FiscalYearEnd    ?? DBNull.Value;
            pState.Value   = (object?)u.StateOfIncorporation ?? DBNull.Value;
            cmd.ExecuteNonQuery();
            written++;

            if (written % 1000 == 0)
                progress?.Report($"SEC Bulk Submissions: Wrote {written:N0} / {updates.Count:N0}...");
        }

        txn.Commit();
    }

    public static string NormalizeSec(string secNumber) =>
        secNumber.Trim().ToUpperInvariant();

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}

/// <summary>Holds EDGAR metadata to be written back to a Firm record.</summary>
public sealed record EdgarFirmUpdate(
    string Crd,
    string Cik,
    string? SicCode,
    string? SicDescription,
    string? FiscalYearEnd,
    string? StateOfIncorporation);
