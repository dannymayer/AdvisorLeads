using System.Net.Http;
using System.Text.RegularExpressions;

namespace AdvisorLeads.Services;

/// <summary>
/// Downloads and parses the Broker Protocol member firm list maintained by J.S. Held.
/// The list tracks which firms have signed the Broker Protocol, allowing registered
/// representatives to move between firms with client contact information.
/// Weekly updates. Major wirehouses (Morgan Stanley, UBS) have withdrawn.
/// </summary>
public class BrokerProtocolService
{
    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    private const string ProtocolUrl = "https://www.jsheld.com/markets-served/financial-services/broker-recruiting/the-broker-protocol";

    /// <summary>
    /// Downloads the Broker Protocol member list and returns all firm names.
    /// Returns an empty list on error (non-throwing).
    /// </summary>
    public async Task<List<string>> FetchMemberNamesAsync(CancellationToken ct = default)
    {
        try
        {
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            _http.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

            var html = await _http.GetStringAsync(ProtocolUrl, ct);
            return ParseMemberNames(html);
        }
        catch { return new List<string>(); }
    }

    private static List<string> ParseMemberNames(string html)
    {
        var names = new List<string>();

        // The J.S. Held page renders the list as an HTML table or list.
        // Try to find table rows with firm names first.
        var tableMatches = Regex.Matches(html, @"<td[^>]*>\s*([A-Za-z][^<]{2,100}?)\s*</td>",
            RegexOptions.IgnoreCase);
        if (tableMatches.Count > 20)
        {
            foreach (Match m in tableMatches)
            {
                var text = StripTags(m.Groups[1].Value).Trim();
                if (IsValidFirmName(text))
                    names.Add(text);
            }
        }

        // Fallback: extract from <li> tags inside a section about broker protocol members
        if (names.Count < 10)
        {
            var liMatches = Regex.Matches(html, @"<li[^>]*>\s*([A-Za-z][^<]{2,100}?)\s*</li>",
                RegexOptions.IgnoreCase);
            foreach (Match m in liMatches)
            {
                var text = StripTags(m.Groups[1].Value).Trim();
                if (IsValidFirmName(text))
                    names.Add(text);
            }
        }

        // Deduplicate, sort
        return names.Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .ToList();
    }

    private static string StripTags(string html)
        => Regex.Replace(html, @"<[^>]+>", "").Replace("&amp;", "&").Replace("&nbsp;", " ").Trim();

    private static bool IsValidFirmName(string s)
    {
        if (s.Length < 3 || s.Length > 120) return false;
        // Skip things that look like headers, dates, or navigation text
        if (Regex.IsMatch(s, @"^\d+$")) return false;
        if (s.Contains("click here", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Contains("download", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Contains("©")) return false;
        return true;
    }
}
