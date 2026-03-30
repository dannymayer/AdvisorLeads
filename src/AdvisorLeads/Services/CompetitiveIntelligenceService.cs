using AdvisorLeads.Abstractions;
using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

public record FirmGrowthSnapshot(
    string FirmName,
    string? FirmCrd,
    string? State,
    int? CurrentAdvisorCount,
    int? PriorAdvisorCount,
    int? AdvisorCountChange,
    decimal? CurrentAum,
    decimal? AumChange1Yr,
    decimal? AumChangePct1Yr,
    bool IsGrowing,
    bool IsShrinking);

public class CompetitiveIntelligenceService
{
    private readonly IAdvisorRepository _repo;

    public CompetitiveIntelligenceService(IAdvisorRepository repo)
    {
        _repo = repo;
    }

    public List<FirmGrowthSnapshot> GetFirmGrowthShrinkData(string? state = null, int topN = 50)
    {
        var firms = _repo.GetFirmsForIntelligence(state);
        var snapshots = new List<FirmGrowthSnapshot>();

        foreach (var firm in firms)
        {
            var current = firm.NumberOfAdvisors;
            var prior = firm.PriorAdvisorCount;
            int? change = (current.HasValue && prior.HasValue)
                ? current.Value - prior.Value
                : null;

            var currentAum = (firm.RegulatoryAum ?? 0) + (firm.RegulatoryAumNonDiscretionary ?? 0);
            var aum1YrAgo = _repo.GetFirmAum1YearAgo(firm.CrdNumber);
            decimal? aumChange = null;
            decimal? aumChangePct = null;

            if (aum1YrAgo.HasValue && aum1YrAgo.Value > 0)
            {
                aumChange = currentAum - aum1YrAgo.Value;
                aumChangePct = (currentAum - aum1YrAgo.Value) / aum1YrAgo.Value * 100;
            }

            bool isGrowing = (change.HasValue && change.Value > 0) ||
                             (aumChange.HasValue && aumChange.Value > 0);
            bool isShrinking = (change.HasValue && change.Value < 0) ||
                                (aumChange.HasValue && aumChange.Value < 0);

            snapshots.Add(new FirmGrowthSnapshot(
                firm.Name,
                firm.CrdNumber,
                firm.State,
                current,
                prior,
                change,
                currentAum > 0 ? currentAum : null,
                aumChange,
                aumChangePct,
                isGrowing,
                isShrinking));
        }

        return snapshots
            .OrderBy(s => s.AdvisorCountChange ?? 0)
            .Take(topN)
            .ToList();
    }

    public async Task RefreshHeadcountDeltasAsync(IProgress<string>? progress = null)
    {
        var firms = _repo.GetFirmsForIntelligence(null);
        progress?.Report($"Refreshing headcount deltas for {firms.Count} firms...");
        int updated = 0;

        foreach (var firm in firms)
        {
            var current = firm.NumberOfAdvisors;
            var prior = firm.PriorAdvisorCount;
            if (current.HasValue && prior.HasValue)
            {
                int delta = current.Value - prior.Value;
                _repo.UpdateFirmAdvisorCountChange(firm.Id, delta);
                updated++;
            }
        }

        await Task.CompletedTask;
        progress?.Report($"✓ Updated headcount deltas for {updated} firms.");
    }
}
