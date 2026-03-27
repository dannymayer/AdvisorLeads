using AdvisorLeads.Data;
using AdvisorLeads.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Web;

namespace AdvisorLeads.Services;

/// <summary>
/// Searches SEC EDGAR filings using the EDGAR Full-Text Search System (EFTS).
/// API endpoint: https://efts.sec.gov/LATEST/search-index
/// Free, no API key required. Rate limit: 10 requests/second.
/// 
/// Primarily used to detect M&A signals by searching for specific keywords
/// in filings (e.g., "succession plan", "change of control", "merger").
/// </summary>
public class EdgarSearchService
{
    private static readonly HttpClient _http = new HttpClient();
    private const string SearchUrl = "https://efts.sec.gov/LATEST/search-index";
    private const int RateLimitDelayMs = 150;
    private readonly string _dbPath;

    // Predefined M&A search queries organized by category
    public static readonly Dictionary<string, string[]> MaSearchQueries = new()
    {
        ["M&A Signal"] = new[]
        {
            "\"change of control\"",
            "\"merger agreement\"",
            "\"acquisition of\"",
            "\"purchase agreement\"",
            "\"asset purchase\"",
        },
        ["Succession"] = new[]
        {
            "\"succession plan\"",
            "\"key person\"",
            "\"retirement of\"",
            "\"transition plan\"",
        },
        ["Growth Signal"] = new[]
        {
            "\"strategic partnership\"",
            "\"new office\"",
            "\"expansion\"",
        },
        ["Distress Signal"] = new[]
        {
            "\"material weakness\"",
            "\"going concern\"",
            "\"regulatory action\"",
            "\"consent order\"",
        }
    };

    static EdgarSearchService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "AdvisorLeads/1.0 (advisor-research; contact@advisorleads.com)");
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public EdgarSearchService(string databasePath)
    {
        _dbPath = databasePath;
    }

    /// <summary>
    /// Searches EDGAR filings for a specific query string.
    /// Optionally filters by form type, date range, and company name/CIK.
    /// Returns parsed search results.
    /// </summary>
    public async Task<List<EdgarSearchResult>> SearchAsync(
        string query,
        string? category = null,
        string? formType = null,
        string? companyName = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int maxResults = 50,
        CancellationToken ct = default)
    {
        var queryParams = new List<string>
        {
            $"q={HttpUtility.UrlEncode(query)}"
        };

        if (!string.IsNullOrEmpty(formType))
            queryParams.Add($"forms={HttpUtility.UrlEncode(formType)}");

        if (!string.IsNullOrEmpty(companyName))
            queryParams.Add($"company={HttpUtility.UrlEncode(companyName)}");

        if (startDate.HasValue || endDate.HasValue)
        {
            queryParams.Add("dateRange=custom");
            if (startDate.HasValue)
                queryParams.Add($"startdt={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue)
                queryParams.Add($"enddt={endDate.Value:yyyy-MM-dd}");
        }

        var url = $"{SearchUrl}?{string.Join("&", queryParams)}";

        try
        {
            var response = await _http.GetStringAsync(url, ct);
            var json = JObject.Parse(response);
            var results = new List<EdgarSearchResult>();

            var hits = json["hits"]?["hits"] as JArray;
            if (hits == null) return results;

            int count = 0;
            foreach (var hit in hits)
            {
                if (count >= maxResults) break;

                var source = hit["_source"];
                if (source == null) continue;

                var result = new EdgarSearchResult
                {
                    CompanyName = source["entity_name"]?.ToString()
                        ?? source["display_names"]?.FirstOrDefault()?.ToString()
                        ?? "Unknown",
                    Cik = source["entity_id"]?.ToString(),
                    FormType = source["form_type"]?.ToString()
                        ?? source["file_type"]?.ToString(),
                    SearchQuery = query,
                    Category = category ?? "General",
                    Snippet = source["_highlight"]?.ToString()
                        ?? source["file_description"]?.ToString(),
                    RelevanceScore = hit["_score"]?.Value<double>(),
                };

                // Parse filing date
                var dateStr = source["file_date"]?.ToString()
                    ?? source["period_of_report"]?.ToString();
                if (DateTime.TryParse(dateStr, out var fd))
                    result.FilingDate = fd;

                // Build accession number and URL
                var accession = source["accession_no"]?.ToString();
                if (!string.IsNullOrEmpty(accession))
                {
                    result.AccessionNumber = accession;
                    var cik = result.Cik;
                    var accNoDashes = accession.Replace("-", "");
                    result.FilingUrl = $"https://www.sec.gov/Archives/edgar/data/{cik}/{accNoDashes}/";
                }

                results.Add(result);
                count++;
            }

            return results;
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// Runs all predefined M&A search queries and stores results.
    /// Targets investment adviser filings from the past year by default.
    /// Returns total number of new results found.
    /// </summary>
    public async Task<int> RunMaSearchScanAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default,
        int monthsBack = 12)
    {
        var startDate = DateTime.Now.AddMonths(-monthsBack);
        int totalNew = 0;

        using var ctx = new DatabaseContext(_dbPath);

        foreach (var (category, queries) in MaSearchQueries)
        {
            foreach (var query in queries)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"EDGAR Search: Scanning for {category} — {query}...");

                var results = await SearchAsync(
                    query, category,
                    formType: "ADV,ADV/A,ADV-W",
                    startDate: startDate,
                    ct: ct);

                // Only store results we haven't seen before
                foreach (var result in results)
                {
                    if (string.IsNullOrEmpty(result.AccessionNumber)) continue;

                    var exists = ctx.EdgarSearchResults
                        .Any(r => r.AccessionNumber == result.AccessionNumber
                            && r.SearchQuery == result.SearchQuery);

                    if (!exists)
                    {
                        result.CreatedAt = DateTime.UtcNow;
                        ctx.EdgarSearchResults.Add(result);
                        totalNew++;
                    }
                }

                ctx.SaveChanges();
                await Task.Delay(RateLimitDelayMs, ct);
            }
        }

        progress?.Report($"EDGAR Search: Found {totalNew} new M&A signal results.");
        return totalNew;
    }

    /// <summary>
    /// Searches for filings mentioning a specific firm by name.
    /// Useful for finding if a firm appears in other companies' filings.
    /// </summary>
    public async Task<List<EdgarSearchResult>> SearchByFirmNameAsync(
        string firmName,
        string? firmCrd = null,
        CancellationToken ct = default)
    {
        var results = await SearchAsync(
            $"\"{firmName}\"",
            category: "Firm Mention",
            startDate: DateTime.Now.AddYears(-2),
            ct: ct);

        // Tag results with firmCrd if provided
        if (!string.IsNullOrEmpty(firmCrd))
        {
            foreach (var r in results)
                r.FirmCrd = firmCrd;
        }

        return results;
    }

    /// <summary>
    /// Gets stored search results for a firm, ordered by filing date descending.
    /// </summary>
    public List<EdgarSearchResult> GetResultsForFirm(string firmCrd)
    {
        using var ctx = new DatabaseContext(_dbPath);
        return ctx.EdgarSearchResults
            .AsNoTracking()
            .Where(r => r.FirmCrd == firmCrd)
            .OrderByDescending(r => r.FilingDate)
            .ToList();
    }

    /// <summary>
    /// Gets all stored search results grouped by category.
    /// </summary>
    public Dictionary<string, List<EdgarSearchResult>> GetResultsByCategory(int maxPerCategory = 20)
    {
        using var ctx = new DatabaseContext(_dbPath);
        var results = ctx.EdgarSearchResults
            .AsNoTracking()
            .OrderByDescending(r => r.FilingDate)
            .ToList();

        return results
            .GroupBy(r => r.Category ?? "General")
            .ToDictionary(
                g => g.Key,
                g => g.Take(maxPerCategory).ToList());
    }
}
