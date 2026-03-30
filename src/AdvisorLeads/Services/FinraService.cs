using System.Net.Http;
using System.Text.RegularExpressions;
using AdvisorLeads.Abstractions;
using Newtonsoft.Json.Linq;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Fetches individual broker/adviser records from the FINRA BrokerCheck public JSON API.
/// API base: https://api.brokercheck.finra.org
/// </summary>
public class FinraService : IFinraProvider, IAdvisorDataSource
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
    /// Automatically paginates through all available results up to <paramref name="maxResults"/>.
    /// </summary>
    public async Task<List<Advisor>> SearchAdvisorsAsync(string query, string? state = null,
        int from = 0, int size = 100, IProgress<string>? progress = null, int maxResults = 10000)
    {
        progress?.Report($"Searching FINRA for \"{query}\"...");

        var advisors = new List<Advisor>();

        try
        {
            int start = from;

            while (advisors.Count < maxResults)
            {
                var url = $"{ApiBase}/search/individual" +
                          $"?query={Uri.EscapeDataString(query)}" +
                          $"&hl=true&includePrevious=true" +
                          $"&nrows={size}&start={start}&r=25&wt=json";

                var json = await _http.GetStringAsync(url);
                var root = JObject.Parse(json);
                var hitsObj = root["hits"] as JObject;
                var hits = hitsObj?["hits"] as JArray;

                if (hits == null || hits.Count == 0)
                    break;

                // Parse total available results from the response
                var totalToken = hitsObj?["total"];
                int totalAvailable = (totalToken as JObject)?["value"]?.Value<int>()
                    ?? totalToken?.Value<int?>() ?? 0;

                foreach (var hit in hits)
                {
                    var advisor = ParseHit(hit);
                    if (advisor == null) continue;

                    if (!string.IsNullOrWhiteSpace(state) &&
                        !string.Equals(advisor.State, state, StringComparison.OrdinalIgnoreCase))
                        continue;

                    advisors.Add(advisor);
                }

                progress?.Report($"FINRA: fetched {advisors.Count}" +
                    (totalAvailable > 0 ? $" of {totalAvailable} result(s)..." : " result(s)..."));

                // Stop if we've fetched all available results or hit the cap
                if (hits.Count < size || (totalAvailable > 0 && start + size >= totalAvailable))
                    break;

                start += size;
                await Task.Delay(300);
            }

            progress?.Report($"✓ Found {advisors.Count} result(s) from FINRA.");
            return advisors;
        }
        catch (Exception ex)
        {
            progress?.Report($"FINRA search error: {ex.Message}");
            return advisors; // Return what we have so far rather than an empty list
        }
    }

    /// <summary>
    /// Issues a GET request with exponential backoff retry on HTTP 429 (Too Many Requests).
    /// Respects the Retry-After header when present; otherwise backs off at 2 s, 4 s, 8 s.
    /// </summary>
    private static async Task<string> GetWithRetryAsync(string url, int maxRetries = 3)
    {
        int delayMs = 2000;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            var response = await _http.GetAsync(url);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();

            if ((int)response.StatusCode == 429)
            {
                if (attempt == maxRetries)
                    response.EnsureSuccessStatusCode(); // throws on final attempt

                // Honour Retry-After header if present
                int wait = delayMs;
                if (response.Headers.TryGetValues("Retry-After", out var vals)
                    && int.TryParse(vals.FirstOrDefault(), out int retryAfterSec))
                {
                    wait = retryAfterSec * 1000 + 500;
                }

                await Task.Delay(wait);
                delayMs *= 2; // exponential backoff
                continue;
            }

            response.EnsureSuccessStatusCode(); // throws for other non-success codes
        }

        throw new InvalidOperationException("Retry loop exhausted unexpectedly.");
    }

    /// <summary>
    /// Fetches full details for a single advisor by CRD number.
    /// Uses /search/individual/{crd} which returns _source.content as rich nested JSON.
    /// Includes a 500 ms base delay and exponential backoff retry on HTTP 429.
    /// </summary>
    public async Task<Advisor?> GetAdvisorDetailAsync(string crd, IProgress<string>? progress = null)
    {
        // Base throttle: 500 ms between every detail call to stay well under FINRA's rate limit.
        await Task.Delay(500);

        try
        {
            // /search/individual/{crd} returns _source.content as a nested JSON string with rich data
            var url = $"{ApiBase}/search/individual/{Uri.EscapeDataString(crd)}";
            var json = await GetWithRetryAsync(url);
            var root = JObject.Parse(json);
            var hits = (root["hits"] as JObject)?["hits"] as JArray;

            if (hits != null && hits.Count > 0)
            {
                // Prefer the hit that matches the requested CRD
                var hit = hits.FirstOrDefault(h =>
                {
                    var srcObj = h["_source"] as JObject;
                    var contentStr = srcObj?["content"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(contentStr) && contentStr.TrimStart().StartsWith("{"))
                    {
                        try
                        {
                            var id = (JObject.Parse(contentStr) as JObject)?["basicInformation"]
                                ?["individualId"]?.ToString();
                            return string.Equals(id, crd, StringComparison.OrdinalIgnoreCase);
                        }
                        catch { }
                    }
                    return string.Equals(srcObj?["ind_source_id"]?.ToString(), crd, StringComparison.OrdinalIgnoreCase);
                }) ?? hits[0];

                var src = hit["_source"] as JObject;
                var contentStr2 = src?["content"]?.ToString();
                if (!string.IsNullOrEmpty(contentStr2))
                    return ParseDetailContent(crd, contentStr2);

                return ParseHit(hit);
            }

            // Fallback: search by CRD
            var searchUrl = $"{ApiBase}/search/individual?query={Uri.EscapeDataString(crd)}&hl=true&includePrevious=true&nrows=5&start=0&r=25&wt=json";
            var searchJson = await GetWithRetryAsync(searchUrl);
            var searchRoot = JObject.Parse(searchJson);
            var searchHitsObj = searchRoot["hits"] as JObject;
            var searchHits = searchHitsObj?["hits"] as JArray;
            if (searchHits == null || searchHits.Count == 0) return null;
            foreach (var h in searchHits)
            {
                if (string.Equals((h["_source"] as JObject)?["ind_source_id"]?.ToString(), crd, StringComparison.OrdinalIgnoreCase))
                    return ParseHit(h);
            }
            return ParseHit(searchHits[0]);
        }
        catch (Exception ex)
        {
            progress?.Report($"FINRA detail error for CRD {crd}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses the rich nested JSON returned in _source.content from /search/individual/{crd}.
    /// Provides employment history with dates, exams/qualifications, and registered states.
    /// </summary>
    private static Advisor? ParseDetailContent(string crd, string contentJson)
    {
        try
        {
            var c = JObject.Parse(contentJson);
            var basic = c["basicInformation"];
            if (basic == null) return null;

            var firstName = basic["firstName"]?.ToString()?.Trim() ?? "";
            var lastName = basic["lastName"]?.ToString()?.Trim() ?? "";
            var middleName = basic["middleName"]?.ToString()?.Trim();

            if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName)) return null;

            var bcScope = basic["bcScope"]?.ToString() ?? "";
            var iaScope = basic["iaScope"]?.ToString() ?? "";
            bool isActiveBC = NormalizeStatus(bcScope) == "Active";
            bool isActiveIA = NormalizeStatus(iaScope) == "Active";
            string? registrationLevel = isActiveIA && isActiveBC ? "Dual"
                : isActiveIA ? "Federal"
                : isActiveBC ? "State"
                : null;

            var disclosureFlag= c["disclosureFlag"]?.ToString() ?? "";
            var iaDisclosureFlag = c["iaDisclosureFlag"]?.ToString() ?? "";
            var hasDisclosures = string.Equals(disclosureFlag, "Y", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(iaDisclosureFlag, "Y", StringComparison.OrdinalIgnoreCase);

            string recordType = isActiveIA ? "Investment Advisor Representative" : "Registered Representative";
            string regStatus = (isActiveIA || isActiveBC) ? "Active"
                : NormalizeStatus(bcScope.Length > iaScope.Length ? bcScope : iaScope);

            var industryDate = basic["daysInIndustryCalculatedDate"]?.ToString();
            DateTime? registrationDate = null;
            int? yearsExp = null;
            if (DateTime.TryParse(industryDate, out var indDate))
            {
                registrationDate = indDate;
                yearsExp = (int)((DateTime.Today - indDate).TotalDays / 365.25);
            }

            // Other names
            var otherNamesArr = basic["otherNames"] as JArray;
            string? otherNames = null;
            if (otherNamesArr != null && otherNamesArr.Count > 0)
                otherNames = string.Join(", ", otherNamesArr
                    .Select(n => TitleCase(n.ToString().Trim()))
                    .Where(n => !string.IsNullOrEmpty(n)));

            // Employment tracking
            var currentFirm = ""; var currentFirmCrd = ""; var currentState = "";
            var currentCity = ""; var currentZip = "";
            var employmentHistory = new List<EmploymentHistory>();

            void AddCurrentEmp(JToken emp)
            {
                var firmName = emp["firmName"]?.ToString()?.Trim()
                    ?? emp["orgName"]?.ToString()?.Trim()
                    ?? emp["businessName"]?.ToString()?.Trim()
                    ?? "";
                var firmId = emp["firmId"]?.ToString()?.Trim()
                    ?? emp["iaFirmCrd"]?.ToString()?.Trim()
                    ?? emp["orgPK"]?.ToString()?.Trim()
                    ?? "";
                var iaOnly = emp["iaOnly"]?.ToString()?.Trim() ?? "";
                var beginDate = emp["registrationBeginDate"]?.ToString();
                DateTime? startDt = DateTime.TryParse(beginDate, out var sd) ? sd : (DateTime?)null;

                string street = "", city = "", state = "", zip = "";
                var branchLocs = emp["branchOfficeLocations"] as JArray;
                if (branchLocs != null && branchLocs.Count > 0)
                {
                    var loc = branchLocs[0];
                    street = loc["street1"]?.ToString()?.Trim() ?? "";
                    city = loc["city"]?.ToString()?.Trim() ?? "";
                    state = loc["state"]?.ToString()?.Trim() ?? "";
                    zip = loc["zipCode"]?.ToString()?.Trim() ?? "";
                }

                if (!string.IsNullOrEmpty(firmName) && string.IsNullOrEmpty(currentFirm))
                {
                    currentFirm = firmName; currentFirmCrd = firmId;
                    currentState = state; currentCity = city; currentZip = zip;
                }

                if (!string.IsNullOrEmpty(firmName) &&
                    !employmentHistory.Any(e => string.Equals(e.FirmName, firmName, StringComparison.OrdinalIgnoreCase)))
                {
                    employmentHistory.Add(new EmploymentHistory
                    {
                        FirmName = firmName,
                        FirmCrd = string.IsNullOrEmpty(firmId) ? null : firmId,
                        StartDate = startDt,
                        EndDate = null,
                        Street = string.IsNullOrWhiteSpace(street) ? null : TitleCase(street),
                        FirmCity = string.IsNullOrWhiteSpace(city) ? null : TitleCase(city),
                        FirmState = string.IsNullOrWhiteSpace(state) ? null : state,
                        Position = iaOnly == "Y" ? "Investment Adviser Representative" : "Registered Representative"
                    });
                }
            }

            void AddPrevEmp(JToken emp)
            {
                var firmName = emp["firmName"]?.ToString()?.Trim()
                    ?? emp["orgName"]?.ToString()?.Trim()
                    ?? emp["businessName"]?.ToString()?.Trim()
                    ?? "";
                var firmId = emp["firmId"]?.ToString()?.Trim() ?? "";
                var iaOnly = emp["iaOnly"]?.ToString()?.Trim() ?? "";
                var beginDate = emp["registrationBeginDate"]?.ToString();
                var endDate = emp["registrationEndDate"]?.ToString();
                DateTime? startDt = DateTime.TryParse(beginDate, out var sd) ? sd : (DateTime?)null;
                DateTime? endDt = DateTime.TryParse(endDate, out var ed) ? ed : (DateTime?)null;

                if (!string.IsNullOrEmpty(firmName) &&
                    !employmentHistory.Any(e => string.Equals(e.FirmName, firmName, StringComparison.OrdinalIgnoreCase)))
                {
                    employmentHistory.Add(new EmploymentHistory
                    {
                        FirmName = firmName,
                        FirmCrd = string.IsNullOrEmpty(firmId) ? null : firmId,
                        StartDate = startDt,
                        EndDate = endDt ?? DateTime.MinValue,
                        Position = iaOnly == "Y" ? "Investment Adviser Representative" : "Registered Representative"
                    });
                }
            }

            var currentEmps = c["currentEmployments"] as JArray;
            var currentIAEmps = c["currentIAEmployments"] as JArray;
            if (currentEmps != null) foreach (var emp in currentEmps) AddCurrentEmp(emp);
            if (currentIAEmps != null)
                foreach (var emp in currentIAEmps)
                    if (!employmentHistory.Any(e => string.Equals(e.FirmName, emp["firmName"]?.ToString(), StringComparison.OrdinalIgnoreCase)))
                        AddCurrentEmp(emp);

            var prevEmps = c["previousEmployments"] as JArray;
            var prevIAEmps = c["previousIAEmployments"] as JArray;
            if (prevEmps != null) foreach (var emp in prevEmps) AddPrevEmp(emp);
            if (prevIAEmps != null)
                foreach (var emp in prevIAEmps)
                    if (!employmentHistory.Any(e => string.Equals(e.FirmName, emp["firmName"]?.ToString(), StringComparison.OrdinalIgnoreCase)))
                        AddPrevEmp(emp);

            // Exams / qualifications from all three categories
            var qualificationList = new List<Qualification>();
            void AddExams(JArray? arr)
            {
                if (arr == null) return;
                foreach (var exam in arr)
                {
                    var code = exam["examCategory"]?.ToString()?.Trim() ?? "";
                    var name = exam["examName"]?.ToString()?.Trim() ?? "";
                    var dateStr = exam["examTakenDate"]?.ToString();
                    var scope = exam["examScope"]?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(code)) continue;
                    if (qualificationList.Any(q => string.Equals(q.Code, code, StringComparison.OrdinalIgnoreCase))) continue;
                    var qual = new Qualification { Code = code, Name = name, Status = scope ?? "Passed" };
                    if (DateTime.TryParse(dateStr, out var qDate)) qual.Date = qDate;
                    qualificationList.Add(qual);
                }
            }
            AddExams(c["stateExamCategory"] as JArray);
            AddExams(c["principalExamCategory"] as JArray);
            AddExams(c["productExamCategory"] as JArray);

            // Registered states → comma-joined for RegAuthorities
            var regStates = c["registeredStates"] as JArray;
            string? regAuthorities = null;
            if (regStates != null && regStates.Count > 0)
            {
                var stateList = regStates
                    .Select(s => s["state"]?.ToString()?.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s)
                    .ToList();
                if (stateList.Count > 0)
                    regAuthorities = string.Join(", ", stateList);
            }

            var registrationList = new List<AdvisorRegistration>();
            if (regStates != null)
            {
                foreach (var s in regStates)
                {
                    var stateCode = s["state"]?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(stateCode)) continue;
                    registrationList.Add(new AdvisorRegistration
                    {
                        StateCode = stateCode,
                        RegistrationCategory = s["regAuthority"]?.ToString()?.Trim() ?? s["category"]?.ToString()?.Trim(),
                        RegistrationStatus = s["status"]?.ToString()?.Trim(),
                        StatusDate = s["statusDate"]?.ToString()?.Trim()
                    });
                }
            }

            // Parse SRO registrations from the sroRegistrations array
            var sroRegs = c["sroRegistrations"] as JArray;
            if (sroRegs != null)
            {
                foreach (var s in sroRegs)
                {
                    var sroName = s["sroName"]?.ToString()?.Trim()
                        ?? s["sro"]?.ToString()?.Trim()
                        ?? s["organization"]?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(sroName)) continue;
                    registrationList.Add(new AdvisorRegistration
                    {
                        RegistrationType = "SRO",
                        SroName = sroName,
                        RegistrationStatus = s["status"]?.ToString()?.Trim()
                            ?? s["registrationStatus"]?.ToString()?.Trim(),
                        StatusDate = s["sinceDate"]?.ToString()?.Trim()
                            ?? s["statusDate"]?.ToString()?.Trim()
                            ?? s["registrationDate"]?.ToString()?.Trim(),
                    });
                }
            }

            // Disclosures
            var disclosureList = new List<Disclosure>();
            var disclosures = c["disclosures"] as JArray;
            if (disclosures != null)
            {
                foreach (var disc in disclosures)
                {
                    var type = disc["disclosureType"]?.ToString()?.Trim()
                            ?? disc["type"]?.ToString()?.Trim() ?? "Disclosure";
                    var description = disc["disclosureDetail"]?.ToString()?.Trim()
                                   ?? disc["description"]?.ToString()?.Trim();
                    var dateStr = disc["disclosureDate"]?.ToString() ?? disc["date"]?.ToString();
                    var resolution = disc["disclosureResolution"]?.ToString()?.Trim()
                                  ?? disc["resolution"]?.ToString()?.Trim();
                    var d = new Disclosure { Type = type, Description = description, Resolution = resolution, Source = "FINRA" };
                    if (DateTime.TryParse(dateStr, out var dd)) d.Date = dd;
                    disclosureList.Add(d);
                }
            }

            var regCount = c["registrationCount"]?.Value<int>() ?? 0;
            string? licenses = qualificationList.Count > 0
                ? string.Join(", ", qualificationList.Select(q => q.Code ?? q.Name).Where(s => !string.IsNullOrEmpty(s)))
                : (regCount > 0 ? $"{regCount} active registration(s)" : null);

            bool hasCriminal = false, hasRegulatory = false, hasCivil = false,
                 hasComplaint = false, hasFinancial = false, hasTermination = false;
            foreach (var disc in disclosureList)
            {
                var t = disc.Type.ToLowerInvariant();
                if (t.Contains("criminal")) hasCriminal = true;
                else if (t.Contains("regulatory") || t.Contains("reg action")) hasRegulatory = true;
                else if (t.Contains("civil")) hasCivil = true;
                else if (t.Contains("customer") || t.Contains("complaint") || t.Contains("arbitration")) hasComplaint = true;
                else if (t.Contains("financial") || t.Contains("bankruptcy") || t.Contains("bankrupt")) hasFinancial = true;
                else if (t.Contains("termination") || t.Contains("separation")) hasTermination = true;
            }

            var disclosureTypes = c["disclosureTypes"] as JArray;
            if (disclosureTypes != null)
            {
                foreach (var dt in disclosureTypes)
                {
                    var t = dt.ToString().ToLowerInvariant();
                    if (t.Contains("criminal")) hasCriminal = true;
                    else if (t.Contains("regulatory")) hasRegulatory = true;
                    else if (t.Contains("civil")) hasCivil = true;
                    else if (t.Contains("customer") || t.Contains("complaint")) hasComplaint = true;
                    else if (t.Contains("financial") || t.Contains("bankruptcy")) hasFinancial = true;
                    else if (t.Contains("termination") || t.Contains("separation")) hasTermination = true;
                }
            }

            int bcDiscCount = c["bcDisclosureCount"]?.Value<int>() ?? disclosureList.Count;
            int iaDiscCount = c["iaDisclosureCount"]?.Value<int>() ?? 0;

            DateTime? careerStartDate = employmentHistory
                .Where(e => e.StartDate.HasValue)
                .Select(e => e.StartDate!.Value)
                .DefaultIfEmpty()
                .Min() is DateTime min && min != default ? min : (DateTime?)null;

            int totalFirmCount = employmentHistory.Count;

            return new Advisor
            {
                CrdNumber = crd,
                FirstName = TitleCase(firstName),
                LastName = TitleCase(lastName),
                MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : TitleCase(middleName),
                OtherNames = otherNames,
                CurrentFirmName = string.IsNullOrWhiteSpace(currentFirm) ? null : TitleCase(currentFirm),
                CurrentFirmCrd = string.IsNullOrWhiteSpace(currentFirmCrd) ? null : currentFirmCrd,
                State = string.IsNullOrWhiteSpace(currentState) ? null : currentState,
                City = string.IsNullOrWhiteSpace(currentCity) ? null : TitleCase(currentCity),
                ZipCode = string.IsNullOrWhiteSpace(currentZip) ? null : currentZip,
                HasDisclosures = hasDisclosures,
                DisclosureCount = disclosureList.Count,
                RegistrationStatus = regStatus,
                RegistrationDate = registrationDate,
                YearsOfExperience = yearsExp,
                Licenses = licenses,
                RegAuthorities = regAuthorities,
                Source = "FINRA",
                RecordType = recordType,
                BcScope = string.IsNullOrWhiteSpace(bcScope) ? null : NormalizeStatus(bcScope),
                IaScope = string.IsNullOrWhiteSpace(iaScope) ? null : NormalizeStatus(iaScope),
                HasCriminalDisclosure = hasCriminal,
                HasRegulatoryDisclosure = hasRegulatory,
                HasCivilDisclosure = hasCivil,
                HasCustomerComplaintDisclosure = hasComplaint,
                HasFinancialDisclosure = hasFinancial,
                HasTerminationDisclosure = hasTermination,
                BcDisclosureCount = bcDiscCount,
                IaDisclosureCount = iaDiscCount,
                RegistrationLevel = registrationLevel,
                BrokerCheckUrl = $"https://brokercheck.finra.org/individual/summary/{crd}",
                BrokerCheckReportPdfUrl = $"https://files.brokercheck.finra.org/individual/individual_{crd}.pdf",
                CareerStartDate = careerStartDate,
                TotalFirmCount = totalFirmCount > 0 ? totalFirmCount : (int?)null,
                EmploymentHistory = employmentHistory,
                QualificationList = qualificationList,
                Disclosures = disclosureList,
                Registrations = registrationList
            };
        }
        catch
        {
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

    // US states and territories for state-based sweep
    private static readonly string[] _stateAbbreviations =
    {
        "AL","AK","AZ","AR","CA","CO","CT","DE","FL","GA","HI","ID","IL","IN","IA",
        "KS","KY","LA","ME","MD","MA","MI","MN","MS","MO","MT","NE","NV","NH","NJ",
        "NM","NY","NC","ND","OH","OK","OR","PA","RI","SC","SD","TN","TX","UT","VT",
        "VA","WA","WV","WI","WY","DC"
    };

    /// <summary>
    /// Fetches a broad batch of advisors for database population using an A–Z alphabet sweep
    /// with pagination on each letter. Each letter can yield up to
    /// <paramref name="maxPagesPerLetter"/> × 100 records; use a smaller value for periodic
    /// refreshes and the default for the initial setup.
    /// </summary>
    public async Task<List<Advisor>> FetchBulkAdvisorsAsync(IProgress<string>? progress = null,
        CancellationToken cancellationToken = default, int maxPagesPerLetter = 50,
        bool activeOnly = false)
    {
        var allAdvisors = new Dictionary<string, Advisor>(StringComparer.OrdinalIgnoreCase);
        const int PageSize = 100;

        // Phase 1: A–Z sweep
        var letters = Enumerable.Range('A', 26).Select(c => ((char)c).ToString()).ToArray();
        int lettersDone = 0;

        foreach (var letter in letters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int start = 0;
            int pagesForLetter = 0;

            while (pagesForLetter < maxPagesPerLetter)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var page = await FetchAdvisorsPageAsync(letter, start, PageSize, cancellationToken, activeOnly);
                    foreach (var a in page)
                    {
                        var key = !string.IsNullOrEmpty(a.CrdNumber) ? a.CrdNumber : $"{a.LastName}:{a.FirstName}";
                        allAdvisors.TryAdd(key, a);
                    }

                    if (page.Count < PageSize) break;

                    start += PageSize;
                    pagesForLetter++;
                }
                catch (OperationCanceledException) { throw; }
                catch { break; }

                await Task.Delay(300, cancellationToken);
            }

            lettersDone++;
            progress?.Report($"FINRA individual sweep: {lettersDone}/26 letters, {allAdvisors.Count} records so far...");

            await Task.Delay(300, cancellationToken);
        }

        // Phase 2: State-based sweep to catch individuals missed by alphabet queries
        // Uses two-letter state code as the query term (state-scoped searches return different result sets)
        int statesDone = 0;
        int maxPagesPerState = Math.Max(1, maxPagesPerLetter / 5); // fewer pages per state

        foreach (var state in _stateAbbreviations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int start = 0;
            int pagesForState = 0;

            while (pagesForState < maxPagesPerState)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Query with state abbreviation — returns individuals whose records prominently mention the state
                    var page = await FetchAdvisorsPageAsync(state, start, PageSize, cancellationToken, activeOnly);
                    int added = 0;
                    foreach (var a in page)
                    {
                        var key = !string.IsNullOrEmpty(a.CrdNumber) ? a.CrdNumber : $"{a.LastName}:{a.FirstName}";
                        if (allAdvisors.TryAdd(key, a)) added++;
                    }

                    if (page.Count < PageSize) break;

                    start += PageSize;
                    pagesForState++;
                }
                catch (OperationCanceledException) { throw; }
                catch { break; }

                await Task.Delay(300, cancellationToken);
            }

            statesDone++;
            if (statesDone % 10 == 0)
                progress?.Report($"FINRA state sweep: {statesDone}/{_stateAbbreviations.Length} states, {allAdvisors.Count} total records...");

            await Task.Delay(300, cancellationToken);
        }

        progress?.Report($"FINRA individual sweep complete: {allAdvisors.Count} unique records.");
        return allAdvisors.Values.ToList();
    }

    /// <summary>
    /// Searches FINRA BrokerCheck for broker-dealer firms matching a query.
    /// </summary>
    public async Task<List<Firm>> SearchFirmsAsync(string query, int from = 0, int size = 12,
        IProgress<string>? progress = null)
    {
        progress?.Report($"Searching FINRA for firm \"{query}\"...");

        try
        {
            var url = $"{ApiBase}/search/firm" +
                      $"?query={Uri.EscapeDataString(query)}" +
                      $"&hl=true&nrows={size}&start={from}&r=25&wt=json";

            var json = await _http.GetStringAsync(url);
            var root = JObject.Parse(json);
            var hits = (root["hits"] as JObject)?["hits"] as JArray;

            if (hits == null || hits.Count == 0)
                return new List<Firm>();

            var firms = new List<Firm>();
            foreach (var hit in hits)
            {
                var firm = ParseFirmHit(hit);
                if (firm != null) firms.Add(firm);
            }

            progress?.Report($"✓ Found {firms.Count} firm(s) from FINRA.");
            return firms;
        }
        catch (Exception ex)
        {
            progress?.Report($"FINRA firm search error: {ex.Message}");
            return new List<Firm>();
        }
    }

    /// <summary>
    /// Fetches a broad batch of broker-dealer firms using an A–Z alphabet sweep with pagination.
    /// </summary>
    public async Task<List<Firm>> FetchBulkFirmsAsync(IProgress<string>? progress = null,
        CancellationToken cancellationToken = default, int maxPagesPerLetter = 30)
    {
        var allFirms = new Dictionary<string, Firm>(StringComparer.OrdinalIgnoreCase);
        const int PageSize = 100;

        var letters = Enumerable.Range('A', 26).Select(c => ((char)c).ToString()).ToArray();
        int lettersDone = 0;

        foreach (var letter in letters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int start = 0;
            int pagesForLetter = 0;

            while (pagesForLetter < maxPagesPerLetter)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var url = $"{ApiBase}/search/firm" +
                              $"?query={Uri.EscapeDataString(letter)}" +
                              $"&hl=true&nrows={PageSize}&start={start}&r=25&wt=json";

                    var json = await _http.GetStringAsync(url);
                    var root = JObject.Parse(json);
                    var hits = (root["hits"] as JObject)?["hits"] as JArray;

                    if (hits == null || hits.Count == 0) break;

                    foreach (var hit in hits)
                    {
                        var firm = ParseFirmHit(hit);
                        if (firm != null && !string.IsNullOrEmpty(firm.CrdNumber))
                            allFirms.TryAdd(firm.CrdNumber, firm);
                    }

                    if (hits.Count < PageSize) break;

                    start += PageSize;
                    pagesForLetter++;
                }
                catch (OperationCanceledException) { throw; }
                catch { break; }

                await Task.Delay(300, cancellationToken);
            }

            lettersDone++;
            progress?.Report($"FINRA firm sweep: {lettersDone}/26 letters, {allFirms.Count} firms so far...");

            await Task.Delay(300, cancellationToken);
        }

        return allFirms.Values.ToList();
    }

    private async Task<List<Advisor>> FetchAdvisorsPageAsync(string query, int start, int size,
        CancellationToken cancellationToken, bool activeOnly = false)
    {
        var url = $"{ApiBase}/search/individual" +
                  $"?query={Uri.EscapeDataString(query)}" +
                  $"&hl=true&includePrevious=true" +
                  $"&nrows={size}&start={start}&r=25&wt=json";

        if (activeOnly)
            url += "&ind_bc_scope=A&ind_ia_scope=A";

        var json = await _http.GetStringAsync(url);
        var root = JObject.Parse(json);
        var hits = (root["hits"] as JObject)?["hits"] as JArray;

        if (hits == null || hits.Count == 0)
            return new List<Advisor>();

        var results = new List<Advisor>();
        foreach (var hit in hits)
        {
            var advisor = ParseHit(hit);
            if (advisor != null) results.Add(advisor);
        }
        return results;
    }

    private static Advisor? ParseHit(JToken hit)
    {
        try
        {
            var src = hit["_source"] as JObject;
            if (src == null) return null;

            var crd = src["ind_source_id"]?.ToString() ?? "";

            // Use the richer content-based parse when full detail is embedded in the hit
            var contentStr = src["content"]?.ToString();
            if (!string.IsNullOrWhiteSpace(contentStr) && contentStr.TrimStart().StartsWith("{"))
            {
                try
                {
                    var detailed = ParseDetailContent(crd, contentStr);
                    if (detailed != null) return detailed;
                }
                catch { }
            }
            var firstName = src["ind_firstname"]?.ToString()?.Trim() ?? "";
            var lastName = src["ind_lastname"]?.ToString()?.Trim() ?? "";
            var middleName = src["ind_middlename"]?.ToString()?.Trim();
            var otherNamesRaw = src["ind_other_names"]?.ToString()?.Trim();
            string? otherNames = !string.IsNullOrWhiteSpace(otherNamesRaw) ? TitleCase(otherNamesRaw) : null;

            if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
                return null;

            var hasDisclosures = string.Equals(
                src["ind_bc_disclosure_fl"]?.ToString(), "Y", StringComparison.OrdinalIgnoreCase);

            var bcScope = src["ind_bc_scope"]?.ToString() ?? "";
            var iaScope = src["ind_ia_scope"]?.ToString() ?? "";

            bool isActiveBC = NormalizeStatus(bcScope) == "Active";
            bool isActiveIA = NormalizeStatus(iaScope) == "Active";

            string recordType;
            if (isActiveIA && isActiveBC)
                recordType = "Investment Advisor Representative";
            else if (isActiveIA)
                recordType = "Investment Advisor Representative";
            else
                recordType = "Registered Representative";

            // Normalize to a consistent status value using the most meaningful scope
            var normBc = NormalizeStatus(bcScope);
            var normIa = NormalizeStatus(iaScope);
            string regStatus = (isActiveIA || isActiveBC) ? "Active"
                : !string.IsNullOrEmpty(normBc) && normBc != "Inactive" ? normBc
                : !string.IsNullOrEmpty(normIa) ? normIa
                : normBc;

            var industryDate = src["ind_industry_cal_date"]?.ToString();
            DateTime? registrationDate = null;
            int? yearsExp = null;
            if (DateTime.TryParse(industryDate, out var indDate))
            {
                registrationDate = indDate;
                yearsExp = (int)((DateTime.Today - indDate).TotalDays / 365.25);
            }

            // Parse current and previous employment.
            var currentFirm = "";
            var currentFirmCrd = "";
            var currentState = "";
            var currentCity = "";
            var currentZip = "";
            var employmentHistory = new List<EmploymentHistory>();

            void ProcessEmpArray(JArray? arr, bool isCurrent)
            {
                if (arr == null) return;
                foreach (var emp in arr)
                {
                    var (firmName, firmId, branchCity, branchState, branchZip, iaOnly) = ParseEmploymentItem(emp);

                    if (isCurrent && string.IsNullOrEmpty(currentFirm) && !string.IsNullOrEmpty(firmName))
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
                            // null EndDate → IsCurrent == true; previous employments use DateTime.MinValue as sentinel
                            EndDate = isCurrent ? null : DateTime.MinValue,
                            Position = iaOnly == "Y" ? "Investment Adviser Representative" : "Registered Representative"
                        });
                    }
                }
            }

            ProcessEmpArray(src["ind_current_employments"] as JArray, isCurrent: true);
            // Previous employments (present in detail responses)
            ProcessEmpArray(src["ind_prev_employments"] as JArray, isCurrent: false);

            // Exams / qualifications (present in detail responses)
            var qualificationList = new List<Qualification>();
            var examArray = src["ind_exams"] as JArray;
            if (examArray != null)
            {
                foreach (var exam in examArray)
                {
                    string examCode, examName, examDate, examStatus;
                    if (exam is JObject eo)
                    {
                        examCode = eo["exam_code"]?.ToString()?.Trim() ?? eo["examCode"]?.ToString()?.Trim() ?? "";
                        examName = eo["exam_name"]?.ToString()?.Trim() ?? eo["examName"]?.ToString()?.Trim() ?? "";
                        examDate = eo["exam_date"]?.ToString()?.Trim() ?? eo["examDate"]?.ToString()?.Trim() ?? "";
                        examStatus = eo["exam_status"]?.ToString()?.Trim() ?? eo["examStatus"]?.ToString()?.Trim() ?? "";
                    }
                    else
                    {
                        var es = exam.ToString();
                        examCode = ExtractField(es, "exam_code");
                        examName = ExtractField(es, "exam_name");
                        examDate = ExtractField(es, "exam_date");
                        examStatus = ExtractField(es, "exam_status");
                    }
                    if (string.IsNullOrWhiteSpace(examName) && string.IsNullOrWhiteSpace(examCode)) continue;
                    var qual = new Qualification { Code = examCode, Name = examName, Status = examStatus };
                    if (DateTime.TryParse(examDate, out var qDate)) qual.Date = qDate;
                    qualificationList.Add(qual);
                }
            }

            var regCount = src["ind_approved_finra_registration_count"]?.Value<int>() ?? 0;
            // Only use "X registration(s)" as license string if no exam-based qualifications found
            string? licenses = qualificationList.Count > 0
                ? string.Join(", ", qualificationList.Select(q => q.Code ?? q.Name).Where(s => !string.IsNullOrEmpty(s)))
                : (regCount > 0 ? $"{regCount} active registration(s)" : null);

            return new Advisor
            {
                CrdNumber = crd,
                FirstName = TitleCase(firstName),
                LastName = TitleCase(lastName),
                MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : TitleCase(middleName),
                OtherNames = otherNames,
                CurrentFirmName = string.IsNullOrWhiteSpace(currentFirm) ? null : TitleCase(currentFirm),
                CurrentFirmCrd = string.IsNullOrWhiteSpace(currentFirmCrd) ? null : currentFirmCrd,
                State = string.IsNullOrWhiteSpace(currentState) ? null : currentState,
                City = string.IsNullOrWhiteSpace(currentCity) ? null : TitleCase(currentCity),
                ZipCode = string.IsNullOrWhiteSpace(currentZip) ? null : currentZip,
                HasDisclosures = hasDisclosures,
                RegistrationStatus = regStatus,
                RegistrationDate = registrationDate,
                YearsOfExperience = yearsExp,
                Licenses = licenses,
                Source = "FINRA",
                RecordType = recordType,
                BcScope = string.IsNullOrWhiteSpace(bcScope) ? null : NormalizeStatus(bcScope),
                IaScope = string.IsNullOrWhiteSpace(iaScope) ? null : NormalizeStatus(iaScope),
                RegistrationLevel = isActiveIA && isActiveBC ? "Dual"
                    : isActiveIA ? "Federal"
                    : isActiveBC ? "State"
                    : null,
                EmploymentHistory = employmentHistory,
                QualificationList = qualificationList
            };
        }
        catch
        {
            return null;
        }
    }

    private static Firm? ParseFirmHit(JToken hit)
    {
        try
        {
            var src = hit["_source"] as JObject;
            if (src == null) return null;

            var crd = src["firm_source_id"]?.ToString() ?? "";
            var name = src["firm_name"]?.ToString()?.Trim() ?? "";

            if (string.IsNullOrEmpty(crd) || string.IsNullOrEmpty(name))
                return null;

            var iaSecNumber = src["firm_ia_full_sec_number"]?.ToString();
            var bdSecNumber = src["firm_bd_full_sec_number"]?.ToString();
            var secNumber = iaSecNumber ?? bdSecNumber;
            var numRegistrations = src["firm_approved_finra_registration_count"]?.Value<int?>();
            var bcScope = src["firm_scope"]?.ToString();
            var iaScope = src["firm_ia_scope"]?.ToString();
            var isActiveBc = string.Equals(bcScope, "ACTIVE", StringComparison.OrdinalIgnoreCase);
            var isActiveIa = string.Equals(iaScope, "ACTIVE", StringComparison.OrdinalIgnoreCase);

            string? city = null, state = null, zip = null, address = null, phone = null;

            // Try firm_ia_address_details first, then firm_address_details
            var addrJson = src["firm_ia_address_details"]?.ToString()
                        ?? src["firm_address_details"]?.ToString();
            if (!string.IsNullOrEmpty(addrJson))
            {
                try
                {
                    var addr = JObject.Parse(addrJson);
                    var office = addr["officeAddress"];
                    if (office != null)
                    {
                        address = office["street1"]?.ToString()?.Trim();
                        city = office["city"]?.ToString()?.Trim();
                        state = office["state"]?.ToString()?.Trim();
                        zip = office["postalCode"]?.ToString()?.Trim();
                    }
                    phone = addr["businessPhoneNumber"]?.ToString()?.Trim();
                }
                catch { }
            }

            var recordType = isActiveIa ? "Investment Adviser" : "Broker-Dealer";

            return new Firm
            {
                CrdNumber = crd,
                Name = TitleCase(name),
                Address = string.IsNullOrWhiteSpace(address) ? null : TitleCase(address),
                City = string.IsNullOrWhiteSpace(city) ? null : TitleCase(city),
                State = state,
                ZipCode = zip,
                Phone = phone,
                SECNumber = secNumber,
                IsRegisteredWithFinra = true,
                IsRegisteredWithSec = !string.IsNullOrEmpty(secNumber),
                NumberOfAdvisors = numRegistrations,
                RegistrationStatus = isActiveBc || isActiveIa ? "ACTIVE" : (bcScope ?? iaScope),
                Source = "FINRA",
                RecordType = recordType
            };
        }
        catch
        {
            return null;
        }
    }

    internal static string NormalizeStatus(string? raw) => raw?.Trim().ToUpperInvariant() switch
    {
        "A" or "ACTIVE" or "APPROVED" => "Active",
        "I" or "INACTIVE" => "Inactive",
        "T" or "TERMINATED" => "Terminated",
        "B" or "BARRED" => "Barred",
        "S" or "SUSPENDED" => "Suspended",
        "NOTINSCOPE" or "" or null => "Inactive",
        _ => raw?.Trim() ?? ""
    };

    private static (string name, string id, string city, string state, string zip, string iaOnly)
        ParseEmploymentItem(JToken emp)
    {
        if (emp is JObject obj)
        {
            return (
                obj["firm_name"]?.ToString()?.Trim() ?? "",
                obj["firm_id"]?.ToString()?.Trim()
                    ?? obj["ia_firm_crd"]?.ToString()?.Trim()
                    ?? obj["firm_crd"]?.ToString()?.Trim()
                    ?? "",
                obj["branch_city"]?.ToString()?.Trim() ?? "",
                obj["branch_state"]?.ToString()?.Trim() ?? "",
                obj["branch_zip"]?.ToString()?.Trim() ?? "",
                obj["ia_only"]?.ToString()?.Trim() ?? ""
            );
        }
        var s = emp.ToString();
        return (ExtractField(s, "firm_name"), ExtractField(s, "firm_id"),
                ExtractField(s, "branch_city"), ExtractField(s, "branch_state"),
                ExtractField(s, "branch_zip"), ExtractField(s, "ia_only"));
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

    // IAdvisorDataSource
    public string SourceTag => "FINRA";

    Task<List<Advisor>> IAdvisorDataSource.SearchAsync(string query, string? state,
        IProgress<string>? progress, CancellationToken token)
        => SearchAdvisorsAsync(query, state, progress: progress);

    Task<Advisor?> IAdvisorDataSource.GetDetailAsync(string crd, IProgress<string>? progress)
        => GetAdvisorDetailAsync(crd, progress);
}

