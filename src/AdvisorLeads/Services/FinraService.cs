using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Service for querying FINRA BrokerCheck API.
/// Docs: https://brokercheck.finra.org/
/// </summary>
public class FinraService
{
    private readonly HttpClient _http;
    private const string SearchUrl = "https://efts.finra.org/EFTS/v2/search-index";
    private const string DetailUrl = "https://api.brokercheck.finra.org/search/individual";

    public FinraService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<List<Advisor>> SearchAdvisorsAsync(string query, string? state = null,
        int from = 0, int size = 12, IProgress<string>? progress = null)
    {
        var url = $"{SearchUrl}?q={Uri.EscapeDataString(query)}&type=Individual&from={from}&size={size}";
        if (!string.IsNullOrWhiteSpace(state))
            url += $"&stateOfLicensure={Uri.EscapeDataString(state)}";

        progress?.Report($"Searching FINRA for \"{query}\"...");

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

            progress?.Report($"Found {advisors.Count} results from FINRA.");
            return advisors;
        }
        catch (HttpRequestException ex)
        {
            progress?.Report($"FINRA search error: {ex.Message}");
            return new List<Advisor>();
        }
    }

    public async Task<Advisor?> GetAdvisorDetailAsync(string crd, IProgress<string>? progress = null)
    {
        progress?.Report($"Fetching FINRA details for CRD #{crd}...");
        try
        {
            var url = $"{DetailUrl}/{crd}?hl=true&includePrevious=true&nrows=12&start=0&wrapup=true";
            var response = await _http.GetStringAsync(url);
            var json = JObject.Parse(response);
            return ParseDetailResponse(json, crd);
        }
        catch (HttpRequestException ex)
        {
            progress?.Report($"FINRA detail error for CRD {crd}: {ex.Message}");
            return null;
        }
    }

    public async Task<List<Advisor>> SearchByFirmAsync(string firmCrd, IProgress<string>? progress = null)
    {
        var url = $"{SearchUrl}?q=*&type=Individual&iac={Uri.EscapeDataString(firmCrd)}&from=0&size=50";
        progress?.Report($"Fetching FINRA advisors for firm CRD {firmCrd}...");
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
            return advisors;
        }
        catch (HttpRequestException ex)
        {
            progress?.Report($"FINRA firm search error: {ex.Message}");
            return new List<Advisor>();
        }
    }

    private static Advisor? ParseSearchHit(JToken src)
    {
        var firstName = src["ind_firstname"]?.ToString()
                     ?? src["firstName"]?.ToString()
                     ?? string.Empty;
        var lastName = src["ind_lastname"]?.ToString()
                    ?? src["lastName"]?.ToString()
                    ?? string.Empty;

        if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
            return null;

        var crd = src["ind_source_id"]?.ToString()
               ?? src["indvlPK"]?.ToString();

        var advisor = new Advisor
        {
            CrdNumber = crd,
            FirstName = firstName,
            LastName = lastName,
            RegistrationStatus = src["ind_bc_scope"]?.ToString()
                              ?? src["registrationStatus"]?.ToString(),
            CurrentFirmName = src["ind_bc_employerregistrytionname"]?.ToString()
                           ?? src["currentEmployer"]?.ToString(),
            CurrentFirmCrd = src["ind_bc_employerregistrycrdnumber"]?.ToString(),
            State = src["ind_bc_stateoflicensure"]?.ToString(),
            HasDisclosures = src["ind_bc_disclosurefl"]?.ToString()?.ToUpper() == "Y",
            Source = "FINRA"
        };

        return advisor;
    }

    private static Advisor? ParseDetailResponse(JObject json, string crd)
    {
        var hits = json["hits"]?["hits"] as JArray;
        JToken? src = null;

        if (hits != null && hits.Count > 0)
            src = hits[0]["_source"];

        if (src == null)
        {
            // Try top-level fields
            src = json["hits"]?["hits"]?[0]?["_source"] ?? json;
        }

        var advisor = new Advisor
        {
            CrdNumber = crd,
            FirstName = src["ind_firstname"]?.ToString() ?? string.Empty,
            LastName = src["ind_lastname"]?.ToString() ?? string.Empty,
            MiddleName = src["ind_middlename"]?.ToString(),
            RegistrationStatus = src["ind_bc_scope"]?.ToString(),
            CurrentFirmName = src["ind_bc_employerregistrytionname"]?.ToString(),
            CurrentFirmCrd = src["ind_bc_employerregistrycrdnumber"]?.ToString(),
            State = src["ind_bc_stateoflicensure"]?.ToString(),
            HasDisclosures = src["ind_bc_disclosurefl"]?.ToString()?.ToUpper() == "Y",
            Source = "FINRA"
        };

        // Parse disclosures
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
                    Source = "FINRA"
                });
            }
        }

        // Parse employment history
        var empHistory = src["registrations"] as JArray ?? src["employmentHistory"] as JArray;
        if (empHistory != null)
        {
            foreach (var e in empHistory)
            {
                advisor.EmploymentHistory.Add(new EmploymentHistory
                {
                    FirmName = e["orgName"]?.ToString() ?? e["firmName"]?.ToString() ?? "Unknown",
                    FirmCrd = e["orgPK"]?.ToString() ?? e["firmCrd"]?.ToString(),
                    StartDate = TryParseDate(e["registrationDate"]?.ToString() ?? e["startDate"]?.ToString()),
                    EndDate = TryParseDate(e["endDate"]?.ToString()),
                    Position = e["regCategory"]?.ToString() ?? e["position"]?.ToString()
                });
            }
        }

        // Parse qualifications/exams
        var exams = src["exams"] as JArray;
        if (exams != null)
        {
            foreach (var exam in exams)
            {
                advisor.QualificationList.Add(new Qualification
                {
                    Name = exam["examName"]?.ToString() ?? string.Empty,
                    Code = exam["examCategory"]?.ToString(),
                    Date = TryParseDate(exam["examDate"]?.ToString()),
                    Status = exam["examStatus"]?.ToString() ?? "Passed"
                });
            }
        }

        // Build licenses string
        var licenses = src["ind_bc_licenses"]?.ToString()
                    ?? string.Join(", ", advisor.QualificationList.Select(q => q.Code ?? q.Name));
        advisor.Licenses = string.IsNullOrEmpty(licenses) ? null : licenses;

        return advisor;
    }

    private static DateTime? TryParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, out var dt)) return dt;
        if (s.Length == 8 && DateTime.TryParseExact(s, "MMddyyyy", null, System.Globalization.DateTimeStyles.None, out var dt2))
            return dt2;
        return null;
    }
}
