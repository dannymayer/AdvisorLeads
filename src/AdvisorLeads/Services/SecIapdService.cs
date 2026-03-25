using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Service for querying the SEC Investment Adviser Public Disclosure (IAPD) database.
/// Docs: https://adviserinfo.sec.gov/
/// </summary>
public class SecIapdService
{
    private static readonly HttpClient _http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    public SecIapdService() { }

    private const string SearchUrl = "https://efts.finra.org/EFTS/v2/search-index";
    private const string DetailUrl = "https://api.brokercheck.finra.org/search/individual";
    private const string IapdSearchUrl = "https://api.adviserinfo.sec.gov/api/Individual/Search";
    private const string IapdDetailUrl = "https://api.adviserinfo.sec.gov/api/Individual";

    public async Task<List<Advisor>> SearchAdvisorsAsync(string query, string? state = null,
        int from = 0, int size = 12, IProgress<string>? progress = null)
    {
        // Use FINRA's unified search with IA type
        var url = $"{SearchUrl}?q={Uri.EscapeDataString(query)}&type=IA&from={from}&size={size}";
        if (!string.IsNullOrWhiteSpace(state))
            url += $"&stateOfLicensure={Uri.EscapeDataString(state)}";

        progress?.Report($"Searching SEC IAPD for \"{query}\"...");

        try
        {
            var response = await _http.GetStringAsync(url);
            var json = JObject.Parse(response);
            var hits = json["hits"]?["hits"] as JArray;
            if (hits == null) return new List<Advisor>();

            var advisors = new List<Advisor>();
            foreach (var hit in hits)
            {
                var src = hit["_source"];
                if (src == null) continue;

                var advisor = ParseSearchHit(src);
                if (advisor != null)
                    advisors.Add(advisor);
            }

            progress?.Report($"Found {advisors.Count} results from SEC IAPD.");
            return advisors;
        }
        catch (HttpRequestException ex)
        {
            progress?.Report($"SEC IAPD search error: {ex.Message}");
            return new List<Advisor>();
        }
    }

    public async Task<Advisor?> GetAdvisorDetailAsync(string crd, IProgress<string>? progress = null)
    {
        progress?.Report($"Fetching SEC IAPD details for CRD #{crd}...");
        try
        {
            // Try FINRA's BrokerCheck API which also covers IA individuals
            var url = $"{DetailUrl}/{crd}?hl=true&includePrevious=true&nrows=12&start=0";
            var response = await _http.GetStringAsync(url);
            var json = JObject.Parse(response);
            return ParseDetailResponse(json, crd);
        }
        catch (HttpRequestException ex)
        {
            progress?.Report($"SEC IAPD detail error for CRD {crd}: {ex.Message}");
            return null;
        }
    }

    private static Advisor? ParseSearchHit(JToken src)
    {
        var firstName = src["ind_firstname"]?.ToString() ?? string.Empty;
        var lastName = src["ind_lastname"]?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
            return null;

        var crd = src["ind_source_id"]?.ToString() ?? src["indvlPK"]?.ToString();

        return new Advisor
        {
            CrdNumber = crd,
            IapdNumber = src["iapd_number"]?.ToString(),
            FirstName = firstName,
            LastName = lastName,
            RegistrationStatus = src["ind_ia_scope"]?.ToString()
                              ?? src["registrationStatus"]?.ToString(),
            CurrentFirmName = src["ind_ia_employerregistrytionname"]?.ToString()
                           ?? src["currentEmployer"]?.ToString(),
            CurrentFirmCrd = src["ind_ia_employerregistrycrdnumber"]?.ToString(),
            State = src["ind_ia_stateoflicensure"]?.ToString(),
            HasDisclosures = src["ind_ia_disclosurefl"]?.ToString()?.ToUpper() == "Y",
            Source = "SEC"
        };
    }

    private static Advisor? ParseDetailResponse(JObject json, string crd)
    {
        var hits = json["hits"]?["hits"] as JArray;
        JToken? src = null;

        if (hits != null && hits.Count > 0)
            src = hits[0]["_source"];

        if (src == null) return null;

        var advisor = new Advisor
        {
            CrdNumber = crd,
            FirstName = src["ind_firstname"]?.ToString() ?? string.Empty,
            LastName = src["ind_lastname"]?.ToString() ?? string.Empty,
            MiddleName = src["ind_middlename"]?.ToString(),
            RegistrationStatus = src["ind_ia_scope"]?.ToString(),
            CurrentFirmName = src["ind_ia_employerregistrytionname"]?.ToString(),
            CurrentFirmCrd = src["ind_ia_employerregistrycrdnumber"]?.ToString(),
            State = src["ind_ia_stateoflicensure"]?.ToString(),
            HasDisclosures = src["ind_ia_disclosurefl"]?.ToString()?.ToUpper() == "Y",
            Source = "SEC"
        };

        var disclosures = src["disclosures"] as JArray;
        if (disclosures != null)
        {
            advisor.DisclosureCount = disclosures.Count;
            foreach (var d in disclosures)
            {
                advisor.Disclosures.Add(new Disclosure
                {
                    Type = d["disclosureType"]?.ToString() ?? "Unknown",
                    Description = d["allegedSanctions"]?.ToString() ?? d["description"]?.ToString(),
                    Date = TryParseDate(d["disclosureDate"]?.ToString()),
                    Resolution = d["disclosureResolution"]?.ToString(),
                    Sanctions = d["sanctions"]?.ToString(),
                    Source = "SEC"
                });
            }
        }

        return advisor;
    }

    private static DateTime? TryParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, out var dt)) return dt;
        return null;
    }
}
