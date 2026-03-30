using AdvisorLeads.Abstractions;
using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Derives enriched firm fields from data already stored in the database
/// (no additional API calls for most fields). Uses the AdvisoryActivities
/// and other firm fields to compute investment strategies, crypto exposure,
/// ownership structure, etc.
/// </summary>
public class FormAdvDeepEnrichmentService
{
    private readonly IAdvisorRepository _repo;

    public FormAdvDeepEnrichmentService(IAdvisorRepository repo)
    {
        _repo = repo;
    }

    /// <summary>
    /// Derives enriched fields from data already stored on the Firm record.
    /// Does not make any network calls.
    /// </summary>
    public void EnrichFirmFromStoredData(Firm firm)
    {
        var activities = (firm.AdvisoryActivities ?? "").ToLowerInvariant();
        var businessType = (firm.BusinessType ?? "").ToLowerInvariant();
        var combined = activities + " " + businessType;

        // Investment strategies — keywords → comma-joined list
        var strategies = new List<string>();
        if (combined.Contains("equity")) strategies.Add("Equity");
        if (combined.Contains("fixed income") || combined.Contains("bond") || combined.Contains("municipal"))
            strategies.Add("Fixed Income");
        if (combined.Contains("real estate")) strategies.Add("Real Estate");
        if (combined.Contains("hedge")) strategies.Add("Hedge");
        if (combined.Contains("crypto") || combined.Contains("digital asset") || combined.Contains("blockchain"))
            strategies.Add("Crypto");
        if (combined.Contains("private equity")) strategies.Add("Private Equity");
        if (strategies.Count > 0)
            firm.InvestmentStrategies = string.Join(",", strategies);

        // Wrap fee programs
        if (combined.Contains("wrap fee"))
            firm.WrapFeePrograms = true;

        // Crypto exposure
        if (combined.Contains("crypto") || combined.Contains("digital asset") || combined.Contains("blockchain"))
            firm.CryptoExposure = true;

        // Direct indexing
        if (combined.Contains("direct index"))
            firm.DirectIndexing = true;

        // Dual registration
        firm.IsDuallyRegistered = firm.IsRegisteredWithSec && firm.IsRegisteredWithFinra;

        // Ownership structure — heuristic from firm name suffix and business type
        var name = (firm.Name ?? "").ToLowerInvariant();
        if (combined.Contains("private equity") || combined.Contains("pe-backed")
            || name.EndsWith(" pe") || combined.Contains("rollup"))
            firm.OwnershipStructure = "PE-Backed";
        else if (combined.Contains("ria rollup") || combined.Contains("ria-rollup") || combined.Contains("aggregator"))
            firm.OwnershipStructure = "RIA-Rollup";
        else if (combined.Contains("bank") || combined.Contains("trust company") || combined.Contains("financial institution"))
            firm.OwnershipStructure = "Bank-Owned";
        else if (name.EndsWith(" llc") || name.EndsWith(" inc") || name.EndsWith(" corp")
            || name.Contains(" & ") || name.EndsWith(" co"))
            firm.OwnershipStructure = "Individual-Owned";
        else
            firm.OwnershipStructure ??= "Other";

        firm.FormAdvDeepEnrichedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Processes firms where FormAdvDeepEnrichedAt is null, up to batchSize.
    /// Returns count of firms enriched.
    /// </summary>
    public Task<int> EnrichBatchAsync(int batchSize = 500, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var filter = new FirmSearchFilter { PageSize = batchSize, PageNumber = 1 };
        var firms = _repo.GetFirms(filter)
            .Where(f => f.FormAdvDeepEnrichedAt == null)
            .Take(batchSize)
            .ToList();

        if (firms.Count == 0)
            return Task.FromResult(0);

        progress?.Report($"Form ADV Deep: enriching {firms.Count} firm(s)...");
        int enriched = 0;

        foreach (var firm in firms)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                EnrichFirmFromStoredData(firm);
                _repo.UpdateFirmEnrichmentData(firm);
                enriched++;
            }
            catch { /* continue on per-firm errors */ }
        }

        if (enriched > 0)
            progress?.Report($"✓ Form ADV Deep: enriched {enriched} firm(s).");

        return Task.FromResult(enriched);
    }
}
