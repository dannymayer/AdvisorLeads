using System.Net.Http;
using Newtonsoft.Json.Linq;
using AdvisorLeads.Abstractions;
using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

public class CourtListenerService
{
    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly IAdvisorRepository _repo;
    private readonly string? _apiToken;

    private const string DocketSearchUrl =
        "https://www.courtlistener.com/api/rest/v4/dockets/";
    private static readonly string[] SecuritiesNatureOfSuit = { "850", "875", "890" };
    private static readonly string[] BankruptcyNatureOfSuit = { "422", "423", "424" };

    // Rate: 5,000/day with token → ~208/hr → 1 req/500ms to be safe
    private const int DelayMs = 500;

    public CourtListenerService(IAdvisorRepository repo, string? apiToken = null)
    {
        _repo = repo;
        _apiToken = apiToken;
        if (!string.IsNullOrEmpty(_apiToken))
        {
            _http.DefaultRequestHeaders.Remove("Authorization");
            _http.DefaultRequestHeaders.Add("Authorization", $"Token {_apiToken}");
        }
        _http.DefaultRequestHeaders.Remove("User-Agent");
        _http.DefaultRequestHeaders.Add("User-Agent",
            "AdvisorLeads/1.0 (recruiter tool; contact@advisorleads.app)");
    }

    /// <summary>
    /// Searches CourtListener for dockets matching an advisor's name.
    /// Filters to securities, bankruptcy, and civil fraud case types.
    /// Returns an empty list when no relevant cases are found.
    /// </summary>
    public async Task<List<AdvisorCourtRecord>> SearchForAdvisorAsync(
        string crd,
        string firstName,
        string lastName,
        string? state = null,
        CancellationToken ct = default)
    {
        var records = new List<AdvisorCourtRecord>();
        var fullName = $"{firstName} {lastName}".Trim();

        foreach (var natureOfSuit in SecuritiesNatureOfSuit.Concat(BankruptcyNatureOfSuit))
        {
            try
            {
                var url = $"{DocketSearchUrl}"
                        + $"?q={Uri.EscapeDataString($"\"{fullName}\"")}"
                        + $"&nature_of_suit={natureOfSuit}"
                        + "&order_by=score+desc&page_size=5";

                var json = await _http.GetStringAsync(url, ct);
                var root = JObject.Parse(json);
                var results = root["results"] as JArray;
                if (results == null) continue;

                foreach (var result in results)
                {
                    string caseName  = result["case_name"]?.ToString() ?? fullName;
                    string? court    = result["court"]?.ToString();
                    string? docket   = result["docket_number"]?.ToString();
                    string? caseUrl  = "https://www.courtlistener.com"
                                     + result["absolute_url"]?.ToString();
                    string? dateStr  = result["date_filed"]?.ToString();
                    DateTime? filed  = DateTime.TryParse(dateStr, out var d) ? d : null;

                    if (!caseName.Contains(lastName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (records.Any(r => r.DocketNumber == docket)) continue;

                    records.Add(new AdvisorCourtRecord
                    {
                        AdvisorCrd   = crd,
                        CaseName     = caseName,
                        Court        = court,
                        FilingDate   = filed,
                        CaseType     = ClassifyCaseType(natureOfSuit),
                        CaseUrl      = caseUrl,
                        DocketNumber = docket,
                        Status       = result["date_terminated"] != null ? "Closed" : "Open",
                        CreatedAt    = DateTime.UtcNow
                    });
                }

                await Task.Delay(DelayMs, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* continue on per-request errors */ }
        }

        return records;
    }

    /// <summary>
    /// Batch enrichment pipeline — processes advisors without a recent court record check.
    /// Returns count of advisors checked.
    /// </summary>
    public async Task<int> EnrichBatchAsync(
        int batchSize = 50,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        int safeBatchSize = Math.Min(batchSize, _apiToken != null ? 200 : 15);
        var advisors = _repo.GetAdvisorsForCourtRecordCheck(safeBatchSize);

        progress?.Report($"Court records: checking {advisors.Count} advisor(s)...");
        int processed = 0;

        foreach (var (crd, id, firstName, lastName, state) in advisors)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var records = await SearchForAdvisorAsync(crd, firstName, lastName, state, ct);
                bool hasRecords = records.Count > 0;

                if (hasRecords)
                {
                    _repo.UpsertAdvisorCourtRecords(crd, id, records);
                    progress?.Report($"  ⚖ {firstName} {lastName}: {records.Count} case(s) found.");
                }

                _repo.UpdateAdvisorCourtRecordFlags(
                    crd,
                    hasFlag: hasRecords,
                    url: records.FirstOrDefault()?.CaseUrl,
                    date: records.OrderByDescending(r => r.FilingDate).FirstOrDefault()?.FilingDate,
                    summary: records.Count > 0 ? $"{records.Count} federal case(s) found" : null,
                    enrichedAt: DateTime.UtcNow);

                processed++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                progress?.Report($"  Court record error for CRD {crd}: {ex.Message}");
            }
        }

        if (processed > 0)
            progress?.Report($"✓ Court records: checked {processed} advisor(s).");
        return processed;
    }

    private static string ClassifyCaseType(string natureOfSuit) => natureOfSuit switch
    {
        "850" or "875" => "SecuritiesViolation",
        "422" or "423" or "424" => "Bankruptcy",
        "470" => "CivilFraud",
        "890" => "OtherCivil",
        _ => "OtherCivil"
    };
}
