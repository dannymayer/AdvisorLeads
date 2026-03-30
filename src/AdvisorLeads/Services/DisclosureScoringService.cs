using AdvisorLeads.Abstractions;
using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

public class DisclosureScoringService
{
    private readonly IAdvisorRepository _repo;

    public DisclosureScoringService(IAdvisorRepository repo)
    {
        _repo = repo;
    }

    private static int GetBaseWeight(string type)
    {
        if (type.Contains("bar", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("permanent", StringComparison.OrdinalIgnoreCase))
            return 100;
        if (type.Contains("criminal", StringComparison.OrdinalIgnoreCase))
            return 90;
        if (type.Contains("suspension", StringComparison.OrdinalIgnoreCase))
            return 80;
        if (type.Contains("revocation", StringComparison.OrdinalIgnoreCase))
            return 75;
        if (type.Contains("judgment", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("award", StringComparison.OrdinalIgnoreCase))
            return 60;
        if (type.Contains("fine", StringComparison.OrdinalIgnoreCase))
            return 50;
        if (type.Contains("complaint", StringComparison.OrdinalIgnoreCase) &&
            (type.Contains("settled", StringComparison.OrdinalIgnoreCase) ||
             type.Contains("settlement", StringComparison.OrdinalIgnoreCase)))
            return 40;
        if (type.Contains("complaint", StringComparison.OrdinalIgnoreCase) &&
            type.Contains("denied", StringComparison.OrdinalIgnoreCase))
            return 15;
        if (type.Contains("complaint", StringComparison.OrdinalIgnoreCase))
            return 30;
        return 25;
    }

    private static double GetRecencyMultiplier(DateTime? date)
    {
        if (!date.HasValue) return 1.0;
        var years = (DateTime.UtcNow - date.Value).TotalDays / 365.25;
        if (years <= 5) return 1.0;
        if (years <= 10) return 0.7;
        return 0.4;
    }

    public int ComputeScore(List<Disclosure> disclosures)
    {
        if (disclosures == null || disclosures.Count == 0) return 0;

        double total = 0;
        foreach (var d in disclosures)
        {
            int weight = GetBaseWeight(d.Type ?? string.Empty);
            if ((d.Type?.Contains("fine", StringComparison.OrdinalIgnoreCase) == true) &&
                IsLargeFine(d))
                weight = 70;

            total += weight * GetRecencyMultiplier(d.Date);
        }

        return (int)Math.Min(100, Math.Round(total / Math.Max(1, disclosures.Count)));
    }

    private static bool IsLargeFine(Disclosure d)
    {
        var text = (d.Sanctions ?? string.Empty) + " " + (d.Description ?? string.Empty);
        var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\$?([\d,]+)");
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            var numStr = m.Groups[1].Value.Replace(",", "");
            if (long.TryParse(numStr, out long val) && val >= 100_000)
                return true;
        }
        return false;
    }

    public async Task RefreshAllScoresAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var advisors = _repo.GetAdvisorsWithDisclosures();
        progress?.Report($"Computing disclosure severity scores for {advisors.Count} advisors with disclosures...");
        int updated = 0;

        foreach (var advisor in advisors)
        {
            ct.ThrowIfCancellationRequested();
            var score = ComputeScore(advisor.Disclosures);
            _repo.UpdateDisclosureSeverityScore(advisor.Id, score);
            updated++;
        }

        await Task.CompletedTask;
        progress?.Report($"✓ Updated disclosure severity scores for {updated} advisors.");
    }
}
