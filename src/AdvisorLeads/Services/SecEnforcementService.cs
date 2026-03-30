using System.Net.Http;
using Newtonsoft.Json.Linq;
using AdvisorLeads.Abstractions;
using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Searches the SEC EDGAR full-text search (EFTS) API for enforcement actions
/// (Admin Proceedings, Litigation Orders, Admin/Cease-and-Desist Orders) matching
/// advisor names, and stores results as SecEnforcementAction records.
/// </summary>
public class SecEnforcementService
{
    private readonly IAdvisorRepository _repo;

    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private const string EftsBase = "https://efts.sec.gov/LATEST/search-index";
    // AP = Admin Proceedings, LO = Litigation Orders, AAO = Accounting/Auditing Orders
    private static readonly string[] EnforcementForms = { "AP", "LO", "AAO" };

    private const int DelayMs = 500;

    static SecEnforcementService()
    {
        _http.DefaultRequestHeaders.Add("User-Agent",
            "AdvisorLeads/1.0 (recruiter tool; contact@advisorleads.app)");
    }

    public SecEnforcementService(IAdvisorRepository repo)
    {
        _repo = repo;
    }

    /// <summary>
    /// Searches SEC EFTS for enforcement actions matching the advisor's name.
    /// </summary>
    public async Task<List<SecEnforcementAction>> SearchForAdvisorAsync(
        string crd, string firstName, string lastName, CancellationToken ct = default)
    {
        var actions = new List<SecEnforcementAction>();
        var fullName = $"{firstName} {lastName}".Trim();
        var formsParam = string.Join(",", EnforcementForms);

        try
        {
            var startYear = DateTime.UtcNow.Year - 15;
            var url = $"{EftsBase}?q={Uri.EscapeDataString($"\"{lastName}\""
                )}&forms={formsParam}&dateRange=custom"
                + $"&startdt={startYear}-01-01&enddt={DateTime.UtcNow:yyyy-MM-dd}";

            var json = await _http.GetStringAsync(url, ct);
            var root = JObject.Parse(json);
            var hits = root["hits"]?["hits"] as JArray;
            if (hits == null) return actions;

            foreach (var hit in hits)
            {
                var src = hit["_source"];
                if (src == null) continue;

                var displayNames = src["display_names"] as JArray;
                bool nameMatch = displayNames?.Any(n =>
                    n.ToString().Contains(lastName, StringComparison.OrdinalIgnoreCase)) == true;

                var entityNames = src["entity_name"]?.ToString() ?? "";
                if (!nameMatch && !entityNames.Contains(lastName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var formType = src["form_type"]?.ToString() ?? "";
                var actionType = formType switch
                {
                    "AP" => "AdminProceeding",
                    "LO" => "LitigatedOrder",
                    "AAO" => "AAO",
                    _ => "AdminProceeding"
                };

                var dateStr = src["file_date"]?.ToString();
                DateTime? fileDate = DateTime.TryParse(dateStr, out var fd) ? fd : null;

                var releaseNum = src["release_num"]?.ToString()
                              ?? src["release_number"]?.ToString();
                var description = src["description"]?.ToString()
                               ?? src["period_of_report"]?.ToString()
                               ?? entityNames;

                var accession = src["accession_no"]?.ToString()?.Replace("-", "");
                var caseUrl = accession != null
                    ? $"https://www.sec.gov/Archives/edgar/data/{accession}"
                    : src["url"]?.ToString();

                actions.Add(new SecEnforcementAction
                {
                    AdvisorCrd = crd,
                    RespondentName = entityNames,
                    ActionType = actionType,
                    FileDate = fileDate,
                    Description = description,
                    CaseUrl = caseUrl,
                    ReleaseNumber = releaseNum,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* return empty list on error */ }

        return actions;
    }

    /// <summary>
    /// Batch enrichment — checks advisors that have not been checked recently.
    /// Returns count of advisors checked.
    /// </summary>
    public async Task<int> EnrichBatchAsync(int batchSize = 50, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var advisors = _repo.GetAdvisorsForSecEnforcementCheck(batchSize);
        if (advisors.Count == 0) return 0;

        progress?.Report($"SEC Enforcement: checking {advisors.Count} advisor(s)...");
        int processed = 0;

        foreach (var (crd, first, last) in advisors)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var actions = await SearchForAdvisorAsync(crd, first, last, ct);
                bool hasAction = actions.Count > 0;

                if (hasAction)
                    _repo.UpsertSecEnforcementActions(crd, actions);

                _repo.UpdateAdvisorSecEnforcementFlags(crd, hasAction, DateTime.UtcNow);
                processed++;

                await Task.Delay(DelayMs, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                progress?.Report($"  SEC enforcement error for CRD {crd}: {ex.Message}");
            }
        }

        if (processed > 0)
            progress?.Report($"✓ SEC Enforcement: checked {processed} advisor(s).");
        return processed;
    }
}
