using AdvisorLeads.Abstractions;
using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

public class MobilityScoreService
{
    private readonly IAdvisorRepository _repo;
    private readonly DisclosureScoringService _disclosureScorer;

    public MobilityScoreService(IAdvisorRepository repo, DisclosureScoringService disclosureScorer)
    {
        _repo = repo;
        _disclosureScorer = disclosureScorer;
    }

    public int ComputeScore(Advisor advisor, Firm? currentFirm, decimal? firmAum1YrAgo)
    {
        int score = 0;

        // 1. Tenure component (max 25 pts)
        if (advisor.CurrentFirmStartDate.HasValue)
        {
            var tenureYears = (DateTime.UtcNow - advisor.CurrentFirmStartDate.Value).TotalDays / 365.25;
            score += tenureYears switch
            {
                <= 2 => 25,
                <= 5 => 15,
                <= 10 => 8,
                _ => 3
            };
        }
        else
        {
            score += 10;
        }

        // 2. Firm change rate (max 20 pts)
        if (advisor.TotalFirmCount.HasValue && advisor.YearsOfExperience.HasValue && advisor.YearsOfExperience.Value > 0)
        {
            double rate = (double)advisor.TotalFirmCount.Value / advisor.YearsOfExperience.Value;
            score += rate switch
            {
                > 0.4 => 20,
                > 0.3 => 15,
                > 0.2 => 10,
                _ => 5
            };
        }

        // 3. Firm AUM trend (max 20 pts)
        if (currentFirm != null && firmAum1YrAgo.HasValue && firmAum1YrAgo.Value > 0)
        {
            var currentAum = (currentFirm.RegulatoryAum ?? 0) + (currentFirm.RegulatoryAumNonDiscretionary ?? 0);
            if (currentAum > 0)
            {
                var pctChange = (double)(currentAum - firmAum1YrAgo.Value) / (double)firmAum1YrAgo.Value * 100;
                score += pctChange switch
                {
                    < -20 => 20,
                    < -10 => 12,
                    < 0 => 5,
                    _ => 0
                };
            }
        }

        // 4. Firm headcount trend (max 15 pts)
        if (currentFirm != null)
        {
            var current = currentFirm.NumberOfAdvisors ?? 0;
            var prior = currentFirm.PriorAdvisorCount ?? current;
            if (current < prior) score += 15;
            else if (current == prior) score += 5;
        }

        // 5. Broker Protocol (max 10 pts)
        if (currentFirm?.BrokerProtocolMember == true)
            score += 10;

        // 6. Disclosure severity (max 10 pts)
        var discScore = advisor.DisclosureSeverityScore ?? 0;
        if (discScore > 50) score += 10;
        else if (discScore >= 25) score += 5;

        return Math.Clamp(score, 0, 100);
    }

    public async Task RefreshAllScoresAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var advisors = _repo.GetActiveAdvisors();
        progress?.Report($"Computing mobility scores for {advisors.Count} active advisors...");
        int updated = 0;

        foreach (var advisor in advisors)
        {
            ct.ThrowIfCancellationRequested();
            Firm? firm = null;
            decimal? aum1YrAgo = null;

            if (!string.IsNullOrEmpty(advisor.CurrentFirmCrd))
            {
                firm = _repo.GetFirmByCrd(advisor.CurrentFirmCrd);
                aum1YrAgo = _repo.GetFirmAum1YearAgo(advisor.CurrentFirmCrd);
            }

            var score = ComputeScore(advisor, firm, aum1YrAgo);
            _repo.UpdateMobilityScore(advisor.Id, score, DateTime.UtcNow);
            updated++;
        }

        await Task.CompletedTask;
        progress?.Report($"✓ Updated mobility scores for {updated} advisors.");
    }
}
