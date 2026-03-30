using Newtonsoft.Json.Linq;
using AdvisorLeads.Abstractions;
using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Enriches advisor records with FINRA sanction data extracted from BrokerCheck disclosure detail.
/// </summary>
public class FinraSanctionService
{
    private readonly IFinraProvider _finra;
    private readonly IAdvisorRepository _repo;

    private const int DelayMs = 400;

    public FinraSanctionService(IFinraProvider finra, IAdvisorRepository repo)
    {
        _finra = finra;
        _repo = repo;
    }

    /// <summary>
    /// Extracts FinraSanction records from the FINRA advisor detail JSON.
    /// Looks for disclosures with regulatory/sanction types and parses fine amounts.
    /// </summary>
    public Task<List<FinraSanction>> ExtractSanctionsFromDetailJson(string crd, JObject detailJson)
    {
        var sanctions = new List<FinraSanction>();

        var disclosures = detailJson["disclosures"] as JArray;
        if (disclosures == null)
            return Task.FromResult(sanctions);

        foreach (var disc in disclosures)
        {
            var type = disc["disclosureType"]?.ToString()?.Trim()
                    ?? disc["type"]?.ToString()?.Trim() ?? "";

            // Only process regulatory / sanction-type disclosures
            var typeLower = type.ToLowerInvariant();
            bool isRegulatory = typeLower.Contains("regulatory") || typeLower.Contains("reg action")
                             || typeLower.Contains("sanction") || typeLower.Contains("fine")
                             || typeLower.Contains("suspension") || typeLower.Contains("bar");
            if (!isRegulatory) continue;

            var description = disc["disclosureDetail"]?.ToString()?.Trim()
                           ?? disc["description"]?.ToString()?.Trim();
            var dateStr = disc["disclosureDate"]?.ToString() ?? disc["date"]?.ToString();
            DateTime? sanctionDate = DateTime.TryParse(dateStr, out var sd) ? sd : null;

            // Parse fine amount from dedicated field or text
            decimal? fineAmount = null;
            var fineToken = disc["fineAmount"] ?? disc["fine_amount"] ?? disc["penaltyAmount"];
            if (fineToken != null && decimal.TryParse(fineToken.ToString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var fa) && fa > 0)
            {
                fineAmount = fa;
            }

            // Determine sanction type
            string sanctionType = "Fine";
            if (typeLower.Contains("bar")) sanctionType = "Bar";
            else if (typeLower.Contains("suspension") || typeLower.Contains("suspend")) sanctionType = "Suspension";
            else if (typeLower.Contains("revocation")) sanctionType = "RevocationOrder";
            else if (fineAmount.HasValue) sanctionType = "Fine";

            // Determine initiating authority
            string? initiatedBy = null;
            var sanctionArr = disc["sanctions"] as JArray;
            if (sanctionArr != null && sanctionArr.Count > 0)
            {
                var first = sanctionArr[0];
                var stType = first["sanctionType"]?.ToString() ?? "";
                initiatedBy = stType.Contains("FINRA") || stType.Contains("finra") ? "FINRA" : "Other";
                if (fineAmount == null)
                {
                    var amt = first["fineAmount"] ?? first["amount"];
                    if (amt != null && decimal.TryParse(amt.ToString(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var sa) && sa > 0)
                        fineAmount = sa;
                }
            }

            var initiatorStr = disc["disclosureInitiatedBy"]?.ToString()
                            ?? disc["initiatedBy"]?.ToString() ?? initiatedBy;
            if (!string.IsNullOrEmpty(initiatorStr))
            {
                initiatedBy = initiatorStr.Contains("FINRA", StringComparison.OrdinalIgnoreCase) ? "FINRA"
                            : initiatorStr.Contains("SEC", StringComparison.OrdinalIgnoreCase) ? "SEC"
                            : initiatorStr.Contains("State", StringComparison.OrdinalIgnoreCase) ? "State"
                            : "Other";
            }

            sanctions.Add(new FinraSanction
            {
                AdvisorCrd = crd,
                SanctionType = sanctionType,
                InitiatedBy = initiatedBy ?? "Other",
                FineAmount = fineAmount,
                SanctionDate = sanctionDate,
                Description = description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        return Task.FromResult(sanctions);
    }

    /// <summary>
    /// Enriches advisors with HasDisclosures=true in batches by fetching their FINRA detail
    /// and extracting sanction records. Returns count of advisors enriched.
    /// </summary>
    public async Task<int> EnrichBatchAsync(int batchSize = 50, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var advisors = _repo.GetAdvisorsForSanctionEnrichment(batchSize);
        if (advisors.Count == 0) return 0;

        progress?.Report($"Sanctions: enriching {advisors.Count} advisor(s)...");
        int enriched = 0;

        foreach (var advisor in advisors)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var detail = await _finra.GetAdvisorDetailAsync(advisor.CrdNumber!);
                if (detail == null)
                {
                    _repo.UpdateAdvisorSanctionFlags(advisor.CrdNumber!, false, null, null, DateTime.UtcNow);
                    continue;
                }

                // Build a synthetic JObject from the parsed disclosures for extraction
                var discArr = new JArray(detail.Disclosures.Select(d => new JObject
                {
                    ["disclosureType"] = d.Type,
                    ["disclosureDetail"] = d.Description,
                    ["disclosureDate"] = d.Date?.ToString("yyyy-MM-dd"),
                    ["disclosureResolution"] = d.Resolution
                }));
                var syntheticJson = new JObject { ["disclosures"] = discArr };

                var sanctions = await ExtractSanctionsFromDetailJson(advisor.CrdNumber!, syntheticJson);

                bool hasActive = sanctions.Any(s => s.IsActive);
                decimal? maxFine = sanctions.Any(s => s.FineAmount.HasValue)
                    ? sanctions.Max(s => s.FineAmount)
                    : null;
                string? topType = sanctions.FirstOrDefault()?.SanctionType;

                if (sanctions.Count > 0)
                    _repo.UpsertAdvisorSanctions(advisor.CrdNumber!, sanctions);

                _repo.UpdateAdvisorSanctionFlags(advisor.CrdNumber!, hasActive, maxFine, topType, DateTime.UtcNow);
                enriched++;

                await Task.Delay(DelayMs, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                progress?.Report($"  Sanction error for CRD {advisor.CrdNumber}: {ex.Message}");
            }
        }

        if (enriched > 0)
            progress?.Report($"✓ Sanctions: enriched {enriched} advisor(s).");
        return enriched;
    }
}
