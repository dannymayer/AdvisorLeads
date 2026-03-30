using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace AdvisorLeads.Services;

public record HunterEmailResult(string Email, int Score, string? FirstName, string? LastName, string? Source);

/// <summary>
/// Service for email enrichment via the Hunter.io API.
/// Docs: https://hunter.io/api-documentation/v2
/// </summary>
public class HunterService
{
    // Static shared client — safe to reuse across requests.
    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly string _apiKey;
    private const string BaseUrl = "https://api.hunter.io/v2";
    private const int MinConfidenceScore = 50;
    // Hunter.io free tier: 10 req/s; 150 ms delay keeps us safely under the limit.
    private const int DelayMs = 150;

    public HunterService(string apiKey)
    {
        _apiKey = apiKey;
    }

    /// <summary>
    /// Strips protocol, www prefix, trailing slashes, and path from a website URL
    /// to produce a bare domain suitable for Hunter.io queries.
    /// </summary>
    public static string ExtractDomain(string website)
    {
        var domain = website.Trim().TrimEnd('/');

        if (domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            domain = domain[8..];
        else if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            domain = domain[7..];

        if (domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            domain = domain[4..];

        // Discard any path component after the domain
        var slashIdx = domain.IndexOf('/');
        if (slashIdx >= 0)
            domain = domain[..slashIdx];

        return domain;
    }

    /// <summary>
    /// Looks up an email address for one advisor using Hunter.io Email Finder.
    /// Returns null when no reliable email is found (score &lt; 50) or on API error.
    /// </summary>
    public async Task<HunterEmailResult?> FindEmailAsync(
        string firstName,
        string lastName,
        string domain,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"{BaseUrl}/email-finder"
                    + $"?first_name={Uri.EscapeDataString(firstName)}"
                    + $"&last_name={Uri.EscapeDataString(lastName)}"
                    + $"&domain={Uri.EscapeDataString(domain)}"
                    + $"&api_key={Uri.EscapeDataString(_apiKey)}";

            var response = await _http.GetAsync(url, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            var result = ParseEmailFinderResponse(json);

            if (result == null || result.Score < MinConfidenceScore)
                return null;

            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns all known email addresses at a domain via Hunter.io Domain Search.
    /// Returns an empty list on API error.
    /// </summary>
    public async Task<List<HunterEmailResult>> DomainSearchAsync(
        string domain,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"{BaseUrl}/domain-search"
                    + $"?domain={Uri.EscapeDataString(domain)}"
                    + $"&api_key={Uri.EscapeDataString(_apiKey)}"
                    + "&limit=10";

            var response = await _http.GetAsync(url, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            return ParseDomainSearchResponse(json);
        }
        catch
        {
            return new List<HunterEmailResult>();
        }
    }

    /// <summary>
    /// Batch-enriches advisors with Hunter.io email lookup.
    /// Calls FindEmailAsync for each entry, reports progress via <paramref name="onProgress"/>,
    /// and invokes <paramref name="onResult"/> for each email found (score ≥ 50).
    /// Returns the total count of emails found.
    /// </summary>
    public async Task<int> EnrichAdvisorsAsync(
        IEnumerable<(int AdvisorId, string FirstName, string LastName, string Domain)> advisors,
        Action<string> onProgress,
        Action<(int AdvisorId, string Email)>? onResult = null,
        CancellationToken ct = default)
    {
        int found = 0;
        var list = advisors.ToList();
        int total = list.Count;
        int processed = 0;

        foreach (var (advisorId, firstName, lastName, domain) in list)
        {
            if (ct.IsCancellationRequested) break;

            processed++;
            onProgress($"Finding email {processed}/{total}: {firstName} {lastName} ({domain})...");

            var result = await FindEmailAsync(firstName, lastName, domain, ct);
            if (result != null)
            {
                found++;
                onResult?.Invoke((advisorId, result.Email));
            }

            await Task.Delay(DelayMs, ct).ConfigureAwait(false);
        }

        return found;
    }

    // ── JSON parsing helpers (public for unit testing) ─────────────────

    /// <summary>
    /// Parses an Email Finder API response.
    /// Returns null when the response contains no email or indicates an error.
    /// Does not apply the confidence score threshold — the caller does that.
    /// </summary>
    public static HunterEmailResult? ParseEmailFinderResponse(string json)
    {
        try
        {
            var obj = JObject.Parse(json);
            var data = obj["data"];
            if (data == null || data.Type == JTokenType.Null)
                return null;

            var email = data["email"]?.ToString();
            if (string.IsNullOrEmpty(email))
                return null;

            var score = data["score"]?.Value<int>() ?? 0;
            var source = data["sources"]?.FirstOrDefault()?["domain"]?.ToString();

            return new HunterEmailResult(email, score, null, null, source);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a Domain Search API response.
    /// Returns only entries whose confidence score meets the threshold.
    /// Returns an empty list on parse error.
    /// </summary>
    public static List<HunterEmailResult> ParseDomainSearchResponse(string json)
    {
        var results = new List<HunterEmailResult>();
        try
        {
            var obj = JObject.Parse(json);
            var emails = obj["data"]?["emails"] as JArray;
            if (emails == null) return results;

            foreach (var e in emails)
            {
                var email = e["value"]?.ToString();
                if (string.IsNullOrEmpty(email)) continue;

                var confidence = e["confidence"]?.Value<int>() ?? 0;
                if (confidence < MinConfidenceScore) continue;

                var first = e["first_name"]?.ToString();
                var last = e["last_name"]?.ToString();
                results.Add(new HunterEmailResult(email, confidence, first, last, null));
            }
        }
        catch { /* return partial/empty results */ }

        return results;
    }
}
