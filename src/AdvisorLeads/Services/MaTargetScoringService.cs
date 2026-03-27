using AdvisorLeads.Data;
using AdvisorLeads.Models;
using Microsoft.EntityFrameworkCore;

namespace AdvisorLeads.Services;

/// <summary>
/// Computes a composite M&A target score (0-100) for RIA firms by analyzing
/// AUM growth, ownership structure, filing activity, compliance history,
/// and other signals from SEC EDGAR data.
/// </summary>
public class MaTargetScoringService
{
    private readonly string _dbPath;
    private readonly AumAnalyticsService _aumAnalytics;
    private readonly ChangeDetectionService _changeDetection;
    private readonly FormAdvHistoricalService _formAdv;
    private readonly EdgarSubmissionsService _edgarSubmissions;
    private readonly EdgarSearchService _edgarSearch;

    public MaTargetScoringService(
        string databasePath,
        AumAnalyticsService aumAnalytics,
        ChangeDetectionService changeDetection,
        FormAdvHistoricalService formAdv,
        EdgarSubmissionsService edgarSubmissions,
        EdgarSearchService edgarSearch)
    {
        _dbPath = databasePath;
        _aumAnalytics = aumAnalytics;
        _changeDetection = changeDetection;
        _formAdv = formAdv;
        _edgarSubmissions = edgarSubmissions;
        _edgarSearch = edgarSearch;
    }

    private DatabaseContext CreateContext() => new DatabaseContext(_dbPath);

    /// <summary>
    /// Calculates the M&A target score for a single firm.
    /// Returns null if insufficient data exists for scoring.
    /// </summary>
    public FirmMaScore? ScoreFirm(string firmCrd)
    {
        using var ctx = CreateContext();
        var firm = ctx.Firms.AsNoTracking().FirstOrDefault(f => f.CrdNumber == firmCrd);
        if (firm == null) return null;

        var score = new FirmMaScore
        {
            FirmCrd = firmCrd,
            FirmName = firm.Name,
            ScoredAt = DateTime.UtcNow,
        };

        double totalPoints = 0;
        double maxPossible = 0;

        // ── 1. AUM Growth Score (max 25 points) ──
        maxPossible += 25;
        var growth = _aumAnalytics.CalculateGrowthMetrics(firmCrd);
        if (growth != null && growth.AumGrowthYoY.HasValue)
        {
            var yoy = growth.AumGrowthYoY.Value;
            if (yoy > 30) { totalPoints += 25; score.AumGrowthPoints = 25; }
            else if (yoy > 15) { totalPoints += 18; score.AumGrowthPoints = 18; }
            else if (yoy > 5) { totalPoints += 10; score.AumGrowthPoints = 10; }
            else if (yoy > 0) { totalPoints += 5; score.AumGrowthPoints = 5; }

            score.AumGrowthDetail = $"YoY: {yoy:+0.0;-0.0}%";
            if (growth.Cagr3Year.HasValue)
                score.AumGrowthDetail += $", 3Y CAGR: {growth.Cagr3Year:+0.0;-0.0}%";
            score.Trend = growth.Trend;
        }

        // ── 2. Firm Size Sweet Spot (max 15 points) ──
        // Mid-size firms ($100M-$2B AUM) are prime acquisition targets
        maxPossible += 15;
        var totalAum = (firm.RegulatoryAum ?? 0) + (firm.RegulatoryAumNonDiscretionary ?? 0);
        if (totalAum > 0)
        {
            if (totalAum >= 100_000_000 && totalAum <= 500_000_000)
            { totalPoints += 15; score.SizePoints = 15; }
            else if (totalAum >= 500_000_000 && totalAum <= 2_000_000_000)
            { totalPoints += 12; score.SizePoints = 12; }
            else if (totalAum >= 50_000_000 && totalAum < 100_000_000)
            { totalPoints += 8; score.SizePoints = 8; }
            else if (totalAum > 2_000_000_000)
            { totalPoints += 5; score.SizePoints = 5; }
            else if (totalAum >= 10_000_000)
            { totalPoints += 3; score.SizePoints = 3; }

            score.SizeDetail = FormatHelpers.FormatAum(totalAum);
        }

        // ── 3. Broker Protocol Membership (max 10 points) ──
        maxPossible += 10;
        if (firm.BrokerProtocolMember)
        {
            totalPoints += 10;
            score.ProtocolPoints = 10;
            score.ProtocolDetail = "Member";
        }

        // ── 4. Clean Compliance Record (max 10 points) ──
        maxPossible += 10;
        var events = _changeDetection.GetEventsForFirm(firmCrd);
        var statusChanges = events.Count(e => e.EventType == "STATUS_CHANGE");
        if (statusChanges == 0 && firm.RegistrationStatus?.Contains("Approved", StringComparison.OrdinalIgnoreCase) == true)
        {
            totalPoints += 10;
            score.CompliancePoints = 10;
            score.ComplianceDetail = "Clean — no status changes, active registration";
        }
        else if (statusChanges <= 1)
        {
            totalPoints += 5;
            score.CompliancePoints = 5;
            score.ComplianceDetail = $"{statusChanges} status change(s)";
        }

        // ── 5. Ownership Concentration (max 10 points) ──
        // Concentrated ownership = easier acquisition path
        maxPossible += 10;
        var ownership = _formAdv.GetFirmOwnership(firmCrd);
        if (ownership.Count > 0)
        {
            var directOwners = ownership.Where(o => o.IsDirectOwner).ToList();
            var maxOwnership = directOwners
                .Where(o => o.OwnershipPercent.HasValue)
                .Select(o => o.OwnershipPercent!.Value)
                .DefaultIfEmpty(0)
                .Max();

            if (maxOwnership >= 75) { totalPoints += 10; score.OwnershipPoints = 10; }
            else if (maxOwnership >= 50) { totalPoints += 7; score.OwnershipPoints = 7; }
            else if (directOwners.Count <= 3) { totalPoints += 5; score.OwnershipPoints = 5; }

            score.OwnershipDetail = $"{directOwners.Count} direct owner(s), max {maxOwnership:F0}%";
        }

        // ── 6. Filing Activity (max 10 points) ──
        // Active filing = well-maintained, professional operation
        maxPossible += 10;
        var filingMetrics = _edgarSubmissions.GetFilingMetrics(firmCrd);
        if (filingMetrics.total > 0)
        {
            if (filingMetrics.amendmentsPerYear >= 2) { totalPoints += 10; score.FilingPoints = 10; }
            else if (filingMetrics.amendmentsPerYear >= 1) { totalPoints += 7; score.FilingPoints = 7; }
            else { totalPoints += 3; score.FilingPoints = 3; }

            score.FilingDetail = $"{filingMetrics.advCount} ADV filings, {filingMetrics.amendmentsPerYear:F1}/yr";
        }

        // ── 7. Recent Change Events (max 10 points) ──
        // Recent positive changes suggest an active, evolving firm
        maxPossible += 10;
        var recentEvents = events.Where(e => e.EventDate > DateTime.UtcNow.AddMonths(-6)).ToList();
        var aumJumps = recentEvents.Count(e => e.EventType is "AUM_JUMP" or "AUM_SURGE");
        var growthSignals = recentEvents.Count(e => e.EventType == "GROWTH_SIGNAL");

        if (aumJumps > 0 || growthSignals > 0)
        {
            var pts = Math.Min(10, aumJumps * 5 + growthSignals * 3);
            totalPoints += pts;
            score.EventPoints = pts;
            score.EventDetail = $"{aumJumps} AUM jump(s), {growthSignals} growth signal(s) in 6 months";
        }

        // Penalty: AUM drops
        var aumDrops = recentEvents.Count(e => e.EventType == "AUM_DROP");
        if (aumDrops > 0)
        {
            var penalty = Math.Min(10, aumDrops * 5);
            totalPoints = Math.Max(0, totalPoints - penalty);
            score.EventDetail += $" [PENALTY: {aumDrops} AUM drop(s)]";
        }

        // ── 8. M&A Keyword Mentions (max 10 points) ──
        maxPossible += 10;
        var searchResults = _edgarSearch.GetResultsForFirm(firmCrd);
        if (searchResults.Count > 0)
        {
            var maSignals = searchResults.Count(r => r.Category == "M&A Signal");
            var succession = searchResults.Count(r => r.Category == "Succession");

            var pts = Math.Min(10, maSignals * 3 + succession * 4);
            totalPoints += pts;
            score.SearchPoints = pts;
            score.SearchDetail = $"{maSignals} M&A mention(s), {succession} succession mention(s)";
        }

        // ── Compute Final Score ──
        score.TotalScore = maxPossible > 0
            ? (int)Math.Round(totalPoints / maxPossible * 100)
            : 0;

        score.TotalScore = Math.Clamp(score.TotalScore, 0, 100);
        score.RawPoints = totalPoints;
        score.MaxPoints = maxPossible;

        score.Grade = score.TotalScore switch
        {
            >= 80 => "A",
            >= 65 => "B",
            >= 50 => "C",
            >= 35 => "D",
            _ => "F"
        };

        return score;
    }

    /// <summary>
    /// Batch-scores all firms with AUM data, returning ranked results.
    /// </summary>
    public List<FirmMaScore> ScoreAllFirms(
        IProgress<string>? progress = null,
        string? stateFilter = null,
        decimal? minAum = null)
    {
        using var ctx = CreateContext();
        IQueryable<Firm> query = ctx.Firms.AsNoTracking()
            .Where(f => !f.IsExcluded && f.RegulatoryAum != null && f.RegulatoryAum > 0);

        if (!string.IsNullOrEmpty(stateFilter))
            query = query.Where(f => f.State == stateFilter);
        if (minAum.HasValue)
            query = query.Where(f => f.RegulatoryAum >= minAum.Value);

        var firmCrds = query.Select(f => f.CrdNumber).ToList();
        progress?.Report($"Scoring {firmCrds.Count} firms...");

        var scores = new List<FirmMaScore>();
        int processed = 0;

        foreach (var crd in firmCrds)
        {
            try
            {
                var score = ScoreFirm(crd);
                if (score != null)
                    scores.Add(score);
            }
            catch { /* skip firms that error */ }

            processed++;
            if (processed % 500 == 0)
                progress?.Report($"Scored {processed:N0} of {firmCrds.Count:N0} firms...");
        }

        progress?.Report($"Scoring complete: {scores.Count:N0} firms scored.");
        return scores.OrderByDescending(s => s.TotalScore).ToList();
    }

    /// <summary>
    /// Gets a summary of score distribution across all scored firms.
    /// </summary>
    public ScoreDistribution GetScoreDistribution(List<FirmMaScore> scores)
    {
        return new ScoreDistribution
        {
            TotalScored = scores.Count,
            GradeA = scores.Count(s => s.Grade == "A"),
            GradeB = scores.Count(s => s.Grade == "B"),
            GradeC = scores.Count(s => s.Grade == "C"),
            GradeD = scores.Count(s => s.Grade == "D"),
            GradeF = scores.Count(s => s.Grade == "F"),
            AverageScore = scores.Count > 0 ? scores.Average(s => s.TotalScore) : 0,
            MedianScore = scores.Count > 0 ? scores.OrderBy(s => s.TotalScore).ElementAt(scores.Count / 2).TotalScore : 0,
        };
    }
}

/// <summary>
/// M&A target score breakdown for a single firm.
/// </summary>
public class FirmMaScore
{
    public string FirmCrd { get; set; } = string.Empty;
    public string FirmName { get; set; } = string.Empty;
    public DateTime ScoredAt { get; set; }

    /// <summary>Final composite score (0-100).</summary>
    public int TotalScore { get; set; }
    /// <summary>Letter grade: A (80+), B (65+), C (50+), D (35+), F (&lt;35).</summary>
    public string Grade { get; set; } = "F";
    /// <summary>AUM trend: "Strong Growth", "Growing", "Stable", "Shrinking", "Declining".</summary>
    public string? Trend { get; set; }

    // Score breakdown (points earned per category)
    public double AumGrowthPoints { get; set; }
    public double SizePoints { get; set; }
    public double ProtocolPoints { get; set; }
    public double CompliancePoints { get; set; }
    public double OwnershipPoints { get; set; }
    public double FilingPoints { get; set; }
    public double EventPoints { get; set; }
    public double SearchPoints { get; set; }
    public double RawPoints { get; set; }
    public double MaxPoints { get; set; }

    // Human-readable detail strings per category
    public string? AumGrowthDetail { get; set; }
    public string? SizeDetail { get; set; }
    public string? ProtocolDetail { get; set; }
    public string? ComplianceDetail { get; set; }
    public string? OwnershipDetail { get; set; }
    public string? FilingDetail { get; set; }
    public string? EventDetail { get; set; }
    public string? SearchDetail { get; set; }
}

/// <summary>Score distribution summary for dashboard display.</summary>
public class ScoreDistribution
{
    public int TotalScored { get; set; }
    public int GradeA { get; set; }
    public int GradeB { get; set; }
    public int GradeC { get; set; }
    public int GradeD { get; set; }
    public int GradeF { get; set; }
    public double AverageScore { get; set; }
    public int MedianScore { get; set; }
}
