using System.Net.Http;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Fetches individual broker/adviser records from the FINRA BrokerCheck public JSON API.
/// API base: https://api.brokercheck.finra.org
/// </summary>
public class FinraService
{
    private static readonly HttpClient _http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        client.DefaultRequestHeaders.Add("Origin", "https://brokercheck.finra.org");
        client.DefaultRequestHeaders.Add("Referer", "https://brokercheck.finra.org/");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private const string ApiBase = "https://api.brokercheck.finra.org";

    /// <summary>
    /// Searches FINRA BrokerCheck for individual advisors matching a query.
    /// </summary>
    public async Task<List<Advisor>> SearchAdvisorsAsync(string query, string? state = null,
        int from = 0, int size = 12, IProgress<string>? progress = null)
    {
        progress?.Report($"Searching FINRA for \"{query}\"...");

        try
        {
            var url = $"{ApiBase}/search/individual" +
                      $"?query={Uri.EscapeDataString(query)}" +
                      $"&hl=true&includePrevious=true" +
                      $"&nrows={size}&start={from}&r=25&wt=json";

            var json = await _http.GetStringAsync(url);
            var root = JObject.Parse(json);
            var hits = root["hits"]?["hits"] as JArray;

            if (hits == null || hits.Count == 0)
            {
                progress?.Report($"FINRA: No results found for \"{query}\".");
                return new List<Advisor>();
            }

            var advisors = new List<Advisor>();
            foreach (var hit in hits)
            {
                var advisor = ParseHit(hit);
                if (advisor != null)
                {
                    if (!string.IsNullOrWhiteSpace(state) &&
                        !string.Equals(advisor.State, state, StringComparison.OrdinalIgnoreCase))
                        continue;

                    advisors.Add(advisor);
                }
            }

            progress?.Report($"✓ Found {advisors.Count} result(s) from FINRA.");
            return advisors;
        }
        catch (Exception ex)
        {
            progress?.Report($"FINRA search error: {ex.Message}");
            return new List<Advisor>();
        }
    }

    /// <summary>
    /// Fetches details for a single advisor by CRD number.
    /// </summary>
    public async Task<Advisor?> GetAdvisorDetailAsync(string crd, IProgress<string>? progress = null)
    {
        try
        {
            var url = $"{ApiBase}/search/individual?query={Uri.EscapeDataString(crd)}" +
                      $"&hl=true&includePrevious=true&nrows=5&start=0&r=25&wt=json";
            var json = await _http.GetStringAsync(url);
            var root = JObject.Parse(json);
            var hits = root["hits"]?["hits"] as JArray;

            if (hits == null || hits.Count == 0)
                return null;

            // Find the exact CRD match.
            foreach (var hit in hits)
            {
                var src = hit["_source"];
                if (src == null) continue;
                if (string.Equals(src["ind_source_id"]?.ToString(), crd, StringComparison.OrdinalIgnoreCase))
                    return ParseHit(hit);
            }

            // Fallback to first result.
            return ParseHit(hits[0]);
        }
        catch (Exception ex)
        {
            progress?.Report($"FINRA detail error for CRD {crd}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Fetches advisors employed at a specific firm.
    /// </summary>
    public async Task<List<Advisor>> SearchByFirmAsync(string firmName, IProgress<string>? progress = null)
    {
        return await SearchAdvisorsAsync(firmName, size: 50, progress: progress);
    }

    /// <summary>
    /// Fetches a broad batch of advisors for initial database population.
    /// Uses common last names and state-based queries to pull a diverse set.
    /// </summary>
    public async Task<List<Advisor>> FetchBulkAdvisorsAsync(IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var allAdvisors = new Dictionary<string, Advisor>(StringComparer.OrdinalIgnoreCase);

        // Fetch by common last name prefixes to get a diverse dataset.
        var seeds = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Miller", "Davis",
                            "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin",
                            "Lee", "Harris", "Clark", "Lewis", "Robinson", "Walker" };

        int total = seeds.Length;
        int done = 0;

        foreach (var seed in seeds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var advisors = await SearchAdvisorsAsync(seed, size: 25, progress: progress);
                foreach (var a in advisors)
                {
                    var key = !string.IsNullOrEmpty(a.CrdNumber) ? a.CrdNumber : $"{a.LastName}:{a.FirstName}";
                    allAdvisors.TryAdd(key, a);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { /* continue with next seed */ }

            done++;
            progress?.Report($"Populating database... ({done}/{total} queries, {allAdvisors.Count} advisors found)");

            // Small delay to be respectful to the API.
            await Task.Delay(300, cancellationToken);
        }

        return allAdvisors.Values.ToList();
    }

    private static Advisor? ParseHit(JToken hit)
    {
        try
        {
            var src = hit["_source"];
            if (src == null) return null;

            var crd = src["ind_source_id"]?.ToString() ?? "";
            var firstName = src["ind_firstname"]?.ToString()?.Trim() ?? "";
            var lastName = src["ind_lastname"]?.ToString()?.Trim() ?? "";
            var middleName = src["ind_middlename"]?.ToString()?.Trim();

            if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
                return null;

            var hasDisclosures = string.Equals(
                src["ind_bc_disclosure_fl"]?.ToString(), "Y", StringComparison.OrdinalIgnoreCase);

            var bcScope = src["ind_bc_scope"]?.ToString() ?? "";
            var iaScope = src["ind_ia_scope"]?.ToString() ?? "";
            var regStatus = bcScope;
            if (!string.IsNullOrEmpty(iaScope) && iaScope != "NotInScope")
                regStatus = string.IsNullOrEmpty(regStatus) ? iaScope : $"{regStatus}, IA: {iaScope}";

            var industryDate = src["ind_industry_cal_date"]?.ToString();
            DateTime? registrationDate = null;
            int? yearsExp = null;
            if (DateTime.TryParse(industryDate, out var indDate))
            {
                registrationDate = indDate;
                yearsExp = (int)((DateTime.Today - indDate).TotalDays / 365.25);
            }

            // Parse current employment from the stringified objects.
            var currentFirm = "";
            var currentFirmCrd = "";
            var currentState = "";
            var currentCity = "";
            var currentZip = "";
            var employmentHistory = new List<EmploymentHistory>();

            var empArray = src["ind_current_employments"] as JArray;
            if (empArray != null && empArray.Count > 0)
            {
                foreach (var emp in empArray)
                {
                    var empStr = emp.ToString();

                    var firmName = ExtractField(empStr, "firm_name");
                    var firmId = ExtractField(empStr, "firm_id");
                    var branchCity = ExtractField(empStr, "branch_city");
                    var branchState = ExtractField(empStr, "branch_state");
                    var branchZip = ExtractField(empStr, "branch_zip");
                    var iaOnly = ExtractField(empStr, "ia_only");

                    if (string.IsNullOrEmpty(currentFirm) && !string.IsNullOrEmpty(firmName))
                    {
                        currentFirm = firmName;
                        currentFirmCrd = firmId;
                        currentState = branchState;
                        currentCity = branchCity;
                        currentZip = branchZip;
                    }

                    if (!string.IsNullOrEmpty(firmName) &&
                        !employmentHistory.Any(e => string.Equals(e.FirmName, firmName, StringComparison.OrdinalIgnoreCase)))
                    {
                        employmentHistory.Add(new EmploymentHistory
                        {
                            FirmName = firmName,
                            FirmCrd = firmId,
                            Position = iaOnly == "Y" ? "Investment Adviser Representative" : "Registered Representative"
                        });
                    }
                }
            }

            var regCount = src["ind_approved_finra_registration_count"]?.Value<int>() ?? 0;
            var licenses = regCount > 0 ? $"{regCount} active registration(s)" : null;

            return new Advisor
            {
                CrdNumber = crd,
                FirstName = TitleCase(firstName),
                LastName = TitleCase(lastName),
                MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : TitleCase(middleName),
                CurrentFirmName = TitleCase(currentFirm),
                CurrentFirmCrd = currentFirmCrd,
                State = currentState,
                City = TitleCase(currentCity),
                ZipCode = currentZip,
                HasDisclosures = hasDisclosures,
                RegistrationStatus = regStatus,
                RegistrationDate = registrationDate,
                YearsOfExperience = yearsExp,
                Licenses = licenses,
                Source = "FINRA",
                EmploymentHistory = employmentHistory
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts a named field value from the PowerShell-style stringified hashtable
    /// format returned by FINRA: "@{key=value; key2=value2}"
    /// </summary>
    private static string ExtractField(string empStr, string fieldName)
    {
        var pattern = $@"{Regex.Escape(fieldName)}=([^;}}]*)";
        var match = Regex.Match(empStr, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private static string TitleCase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower());
    }
}

