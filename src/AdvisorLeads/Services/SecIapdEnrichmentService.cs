using AdvisorLeads.Abstractions;
using AdvisorLeads.Data;
using AdvisorLeads.Models;
using Newtonsoft.Json.Linq;

namespace AdvisorLeads.Services;

/// <summary>
/// Enriches individual advisor records using the SEC IAPD API.
/// Endpoint: https://api.adviserinfo.sec.gov/registration/crd/{CRD}/individual
/// Free, no authentication required. Use 300ms+ delay between calls to avoid rate limiting.
///
/// Used as a fallback when FINRA detail enrichment is incomplete — particularly for
/// SEC-sourced IARs and advisors missing employment history or qualifications.
/// </summary>
public class SecIapdEnrichmentService
{
    private readonly IAdvisorRepository _repo;
    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30),
        BaseAddress = new Uri("https://api.adviserinfo.sec.gov/")
    };

    static SecIapdEnrichmentService()
    {
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public SecIapdEnrichmentService(IAdvisorRepository repo)
    {
        _repo = repo;
    }

    /// <summary>
    /// Enriches up to <paramref name="maxToProcess"/> advisors that are missing
    /// qualifications/employment history and have not been enriched recently.
    /// </summary>
    public async Task<int> EnrichBatchAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default,
        int maxToProcess = 200)
    {
        var crds = _repo.GetCrdsNeedingIapdEnrichment(maxToProcess);
        if (crds.Count == 0) return 0;

        progress?.Report($"SEC IAPD: enriching {crds.Count} advisor records...");
        int enriched = 0;

        foreach (var crd in crds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var advisor = await FetchIndividualAsync(crd, ct);
                if (advisor != null)
                {
                    _repo.UpsertAdvisor(advisor);
                    enriched++;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { /* ignore individual failures */ }

            await Task.Delay(350, ct);

            if (enriched > 0 && enriched % 50 == 0)
                progress?.Report($"  SEC IAPD: enriched {enriched}/{crds.Count}...");
        }

        if (enriched > 0)
            progress?.Report($"✓ SEC IAPD enriched {enriched} advisor records.");
        return enriched;
    }

    /// <summary>
    /// Fetches full individual registration detail from the IAPD API for a given CRD number.
    /// Returns null on error or if the CRD is not found.
    /// </summary>
    private async Task<Advisor?> FetchIndividualAsync(string crd, CancellationToken ct)
    {
        try
        {
            var url = $"registration/crd/{crd}/individual";
            var json = await _http.GetStringAsync(url, ct);
            return ParseIapdResponse(json, crd);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null; // CRD not found in IAPD — skip silently
        }
        catch { return null; }
    }

    /// <summary>
    /// Parses the IAPD individual registration JSON into an Advisor model.
    /// The IAPD response has a different structure than the FINRA BrokerCheck response.
    /// </summary>
    private Advisor? ParseIapdResponse(string json, string crd)
    {
        try
        {
            var root = JObject.Parse(json);

            // The IAPD API typically returns an object with "hits" or a direct registration object.
            // Try both structures:
            JToken? regData = root["hits"]?[0]?["_source"] ?? root;

            var firstName = regData?["ind_firstname"]?.Value<string>()
                         ?? regData?["firstName"]?.Value<string>() ?? "";
            var lastName = regData?["ind_lastname"]?.Value<string>()
                        ?? regData?["lastName"]?.Value<string>() ?? "";

            if (string.IsNullOrEmpty(lastName)) return null;

            var advisor = new Advisor
            {
                CrdNumber = crd,
                FirstName = firstName,
                LastName = lastName,
                MiddleName = regData?["ind_middlename"]?.Value<string>(),
                Source = "SEC IAPD",
                RecordType = "Investment Advisor Representative",
            };

            var status = regData?["ind_status"]?.Value<string>()
                      ?? regData?["registrationStatus"]?.Value<string>();
            advisor.RegistrationStatus = NormalizeStatus(status);

            // Current employment
            var currentEmps = regData?["currentEmployments"] as JArray
                           ?? regData?["employmentHistory"]?["currentEmployments"] as JArray;
            if (currentEmps != null && currentEmps.Count > 0)
            {
                var emp = currentEmps[0];
                advisor.CurrentFirmName = emp["firmName"]?.Value<string>();
                advisor.CurrentFirmCrd = emp["firmId"]?.Value<string>();

                var startDateStr = emp["registrationBeginDate"]?.Value<string>();
                if (!string.IsNullOrEmpty(startDateStr) && DateTime.TryParse(startDateStr, out var sd))
                    advisor.RegistrationDate = sd;

                var branch = emp["branchOfficeLocations"]?[0];
                if (branch != null)
                {
                    advisor.City = branch["city"]?.Value<string>();
                    advisor.State = branch["state"]?.Value<string>();
                    advisor.ZipCode = branch["zipCode"]?.Value<string>();
                }

                foreach (var e in currentEmps)
                {
                    var eh = new EmploymentHistory
                    {
                        FirmName = e["firmName"]?.Value<string>() ?? "",
                        FirmCrd = e["firmId"]?.Value<string>(),
                        Position = e["registrationCategory"]?.Value<string>(),
                    };
                    if (DateTime.TryParse(e["registrationBeginDate"]?.Value<string>(), out var esd))
                        eh.StartDate = esd;
                    advisor.EmploymentHistory.Add(eh);
                }
            }

            // Previous employment
            var prevEmps = regData?["previousEmployments"] as JArray
                        ?? regData?["employmentHistory"]?["previousEmployments"] as JArray;
            if (prevEmps != null)
            {
                foreach (var pe in prevEmps)
                {
                    var eh = new EmploymentHistory
                    {
                        FirmName = pe["firmName"]?.Value<string>() ?? "",
                        FirmCrd = pe["firmId"]?.Value<string>(),
                        Position = pe["registrationCategory"]?.Value<string>(),
                    };
                    if (DateTime.TryParse(pe["registrationBeginDate"]?.Value<string>(), out var sd)) eh.StartDate = sd;
                    if (DateTime.TryParse(pe["registrationEndDate"]?.Value<string>(), out var ed)) eh.EndDate = ed;
                    advisor.EmploymentHistory.Add(eh);
                }
            }

            // Disclosures
            var disclosures = regData?["disclosures"] as JArray;
            if (disclosures != null && disclosures.Count > 0)
            {
                advisor.HasDisclosures = true;
                advisor.DisclosureCount = disclosures.Count;
                foreach (var d in disclosures)
                {
                    var disc = new Disclosure
                    {
                        Type = d["disclosureType"]?.Value<string>()
                            ?? d["type"]?.Value<string>() ?? "",
                        Description = d["disclosureDetail"]?.Value<string>()
                                   ?? d["description"]?.Value<string>(),
                        Source = "SEC IAPD",
                    };
                    if (DateTime.TryParse(d["disclosureDate"]?.Value<string>(), out var dd)) disc.Date = dd;
                    advisor.Disclosures.Add(disc);
                }
            }
            else
            {
                advisor.HasDisclosures = false;
                advisor.DisclosureCount = 0;
            }

            // Qualifications / Exams — gather from all exam category fields
            var allExams = new List<JToken>();
            foreach (var examField in new[] { "stateExamCategory", "principalExamCategory", "productExamCategory" })
            {
                if (regData?[examField] is JArray arr) allExams.AddRange(arr);
            }
            if (allExams.Count == 0 && regData?["examDetails"] is JArray ed2)
                allExams.AddRange(ed2);

            foreach (var exam in allExams)
            {
                var qual = new Qualification
                {
                    Name = exam["examName"]?.Value<string>()
                        ?? exam["name"]?.Value<string>() ?? "",
                    Code = exam["examCategory"]?.Value<string>()
                        ?? exam["code"]?.Value<string>(),
                    Status = exam["examScope"]?.Value<string>() ?? "Passed",
                };
                if (DateTime.TryParse(
                    exam["examTakenDate"]?.Value<string>() ?? exam["date"]?.Value<string>(),
                    out var qd)) qual.Date = qd;
                if (!string.IsNullOrEmpty(qual.Name))
                    advisor.QualificationList.Add(qual);
            }

            if (advisor.QualificationList.Count > 0)
            {
                advisor.Qualifications = string.Join(", ",
                    advisor.QualificationList
                        .Where(q => !string.IsNullOrEmpty(q.Code))
                        .Select(q => q.Code)
                        .Distinct());
            }

            // State registrations / licenses
            var stateRegs = regData?["stateRegistrations"] as JArray
                         ?? regData?["currentStateLicenses"] as JArray;
            if (stateRegs != null && stateRegs.Count > 0)
            {
                advisor.Licenses = string.Join(", ",
                    stateRegs.Select(s => s["state"]?.Value<string>()
                                       ?? s["stateCode"]?.Value<string>())
                             .Where(s => !string.IsNullOrEmpty(s))
                             .Distinct()
                             .OrderBy(s => s));
            }

            // Years of experience from earliest employment
            var allHistory = advisor.EmploymentHistory.Where(e => e.StartDate.HasValue).ToList();
            if (allHistory.Count > 0)
            {
                var earliest = allHistory.Min(e => e.StartDate!.Value);
                advisor.YearsOfExperience = (int)((DateTime.Now - earliest).TotalDays / 365.25);
            }

            return advisor;
        }
        catch { return null; }
    }

    private static string NormalizeStatus(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Inactive";
        return raw.ToUpperInvariant() switch
        {
            "A" or "ACTIVE" or "APPROVED" => "Active",
            "I" or "INACTIVE"             => "Inactive",
            "T" or "TERMINATED"           => "Terminated",
            "B" or "BARRED"               => "Barred",
            "S" or "SUSPENDED"            => "Suspended",
            _                             => "Inactive"
        };
    }
}
