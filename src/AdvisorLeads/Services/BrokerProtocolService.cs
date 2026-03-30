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

    /// <summary>
    /// Parses broker protocol member firm names from raw HTML.
    /// Public for unit-test access; the page format may vary, so multiple
    /// extraction strategies are tried in order of reliability.
    /// </summary>
    public static List<string> ParseMemberNames(string html)
    {
        var names = new List<string>();

        // Strategy 1: HTML table cells — most structured form.
        // Threshold of 5 prevents tiny unrelated tables from triggering this path.
        var tableMatches = Regex.Matches(html, @"<td[^>]*>\s*([A-Za-z][^<]{2,100}?)\s*</td>",
            RegexOptions.IgnoreCase);
        if (tableMatches.Count > 5)
        {
            foreach (Match m in tableMatches)
            {
                var text = StripTags(m.Groups[1].Value).Trim();
                if (IsValidFirmName(text))
                    names.Add(text);
            }
        }

        // Strategy 2: <li> list items
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

        // Strategy 3: <p> paragraph tags (some CMS renderings)
        if (names.Count < 10)
        {
            var pMatches = Regex.Matches(html, @"<p[^>]*>\s*([A-Za-z][^<]{2,100}?)\s*</p>",
                RegexOptions.IgnoreCase);
            foreach (Match m in pMatches)
            {
                var text = StripTags(m.Groups[1].Value).Trim();
                if (IsValidFirmName(text))
                    names.Add(text);
            }
        }

        // Strategy 4: <div> tags with short, name-like text content
        if (names.Count < 10)
        {
            var divMatches = Regex.Matches(html, @"<div[^>]*>\s*([A-Za-z][^<]{2,100}?)\s*</div>",
                RegexOptions.IgnoreCase);
            foreach (Match m in divMatches)
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
        if (Regex.IsMatch(s, @"^\d+$")) return false;

        // Skip navigation, footer, and boilerplate content
        if (s.Contains("J.S. Held", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Contains("Copyright", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Contains("Contact", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Contains("Privacy", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Contains("Terms", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Contains("click here", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Contains("download", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Contains("©")) return false;
        return true;
    }
}
