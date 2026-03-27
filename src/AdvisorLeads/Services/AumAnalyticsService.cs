using AdvisorLeads.Data;
using AdvisorLeads.Models;
using Microsoft.EntityFrameworkCore;

namespace AdvisorLeads.Services;

/// <summary>
/// Provides AUM growth analytics by tracking monthly snapshots and computing
/// year-over-year growth rates, CAGR, and trend classifications.
/// </summary>
public class AumAnalyticsService
{
    private readonly string _dbPath;

    public AumAnalyticsService(string databasePath)
    {
        _dbPath = databasePath;
    }

    private DatabaseContext CreateContext() => new DatabaseContext(_dbPath);

    /// <summary>
    /// Records a monthly AUM snapshot for a batch of firms.
    /// Called after SecMonthlyFirmService processes the monthly CSV.
    /// Skips firms that already have a snapshot for this month.
    /// </summary>
    public int SnapshotFirmData(List<Firm> firms, DateTime snapshotMonth)
    {
        using var ctx = CreateContext();
        var monthStart = new DateTime(snapshotMonth.Year, snapshotMonth.Month, 1);
        var sourceTag = $"SEC_MONTHLY_{monthStart:yyyyMM}";

        // Get existing snapshots for this month to avoid duplicates
        var existingCrds = ctx.Set<FirmAumHistory>()
            .Where(h => h.SnapshotDate == monthStart)
            .Select(h => h.FirmCrd)
            .ToHashSet();

        var newSnapshots = new List<FirmAumHistory>();
        foreach (var firm in firms)
        {
            if (string.IsNullOrEmpty(firm.CrdNumber)) continue;
            if (existingCrds.Contains(firm.CrdNumber)) continue;
            if (!firm.RegulatoryAum.HasValue && !firm.RegulatoryAumNonDiscretionary.HasValue) continue;

            var totalAum = (firm.RegulatoryAum ?? 0) + (firm.RegulatoryAumNonDiscretionary ?? 0);

            newSnapshots.Add(new FirmAumHistory
            {
                FirmCrd = firm.CrdNumber,
                SnapshotDate = monthStart,
                RegulatoryAum = firm.RegulatoryAum,
                RegulatoryAumNonDiscretionary = firm.RegulatoryAumNonDiscretionary,
                TotalAum = totalAum > 0 ? totalAum : null,
                NumberOfEmployees = firm.NumberOfEmployees,
                NumberOfAdvisors = firm.NumberOfAdvisors,
                NumClients = firm.NumClients,
                Source = sourceTag,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (newSnapshots.Count > 0)
        {
            // Batch insert in chunks of 5000
            foreach (var batch in newSnapshots.Chunk(5000))
            {
                ctx.Set<FirmAumHistory>().AddRange(batch);
                ctx.SaveChanges();
            }
        }

        return newSnapshots.Count;
    }

    /// <summary>
    /// Gets all AUM snapshots for a firm, ordered by date ascending.
    /// </summary>
    public List<FirmAumHistory> GetAumHistory(string firmCrd)
    {
        using var ctx = CreateContext();
        return ctx.Set<FirmAumHistory>()
            .AsNoTracking()
            .Where(h => h.FirmCrd == firmCrd)
            .OrderBy(h => h.SnapshotDate)
            .ToList();
    }

    /// <summary>
    /// Calculates growth metrics for a specific firm.
    /// </summary>
    public FirmGrowthMetrics? CalculateGrowthMetrics(string firmCrd)
    {
        var history = GetAumHistory(firmCrd);
        if (history.Count < 2) return null;

        var latest = history.Last();
        var currentAum = latest.TotalAum ?? latest.RegulatoryAum ?? 0;
        if (currentAum == 0) return null;

        var metrics = new FirmGrowthMetrics
        {
            FirmCrd = firmCrd,
            CurrentAum = currentAum,
            LatestSnapshotDate = latest.SnapshotDate,
            SnapshotCount = history.Count,
        };

        // YoY Growth — find snapshot closest to 12 months ago
        var oneYearAgo = latest.SnapshotDate.AddYears(-1);
        var yearAgoSnapshot = history
            .Where(h => h.SnapshotDate <= oneYearAgo)
            .OrderByDescending(h => h.SnapshotDate)
            .FirstOrDefault();

        if (yearAgoSnapshot != null)
        {
            var priorAum = yearAgoSnapshot.TotalAum ?? yearAgoSnapshot.RegulatoryAum ?? 0;
            if (priorAum > 0)
            {
                metrics.AumGrowthYoY = (double)((currentAum - priorAum) / priorAum * 100);
                metrics.AumOneYearAgo = priorAum;
            }
        }

        // 3-Year CAGR
        var threeYearsAgo = latest.SnapshotDate.AddYears(-3);
        var threeYrSnapshot = history
            .Where(h => h.SnapshotDate <= threeYearsAgo)
            .OrderByDescending(h => h.SnapshotDate)
            .FirstOrDefault();

        if (threeYrSnapshot != null)
        {
            var priorAum = threeYrSnapshot.TotalAum ?? threeYrSnapshot.RegulatoryAum ?? 0;
            if (priorAum > 0)
            {
                var years = (latest.SnapshotDate - threeYrSnapshot.SnapshotDate).TotalDays / 365.25;
                if (years > 0.5)
                    metrics.Cagr3Year = (Math.Pow((double)(currentAum / priorAum), 1.0 / years) - 1) * 100;
            }
        }

        // 5-Year CAGR
        var fiveYearsAgo = latest.SnapshotDate.AddYears(-5);
        var fiveYrSnapshot = history
            .Where(h => h.SnapshotDate <= fiveYearsAgo)
            .OrderByDescending(h => h.SnapshotDate)
            .FirstOrDefault();

        if (fiveYrSnapshot != null)
        {
            var priorAum = fiveYrSnapshot.TotalAum ?? fiveYrSnapshot.RegulatoryAum ?? 0;
            if (priorAum > 0)
            {
                var years = (latest.SnapshotDate - fiveYrSnapshot.SnapshotDate).TotalDays / 365.25;
                if (years > 0.5)
                    metrics.Cagr5Year = (Math.Pow((double)(currentAum / priorAum), 1.0 / years) - 1) * 100;
            }
        }

        // Employee/Client growth
        if (yearAgoSnapshot != null)
        {
            if (latest.NumClients.HasValue && yearAgoSnapshot.NumClients.HasValue && yearAgoSnapshot.NumClients > 0)
                metrics.ClientGrowthYoY = (double)(latest.NumClients.Value - yearAgoSnapshot.NumClients.Value) / yearAgoSnapshot.NumClients.Value * 100;

            if (latest.NumberOfEmployees.HasValue && yearAgoSnapshot.NumberOfEmployees.HasValue && yearAgoSnapshot.NumberOfEmployees > 0)
                metrics.EmployeeGrowthYoY = (double)(latest.NumberOfEmployees.Value - yearAgoSnapshot.NumberOfEmployees.Value) / yearAgoSnapshot.NumberOfEmployees.Value * 100;
        }

        // Trend classification based on last 3+ snapshots
        if (history.Count >= 3)
        {
            var recentHistory = history.TakeLast(Math.Min(6, history.Count)).ToList();
            var aumValues = recentHistory
                .Select(h => h.TotalAum ?? h.RegulatoryAum ?? 0)
                .Where(a => a > 0)
                .ToList();

            if (aumValues.Count >= 3)
            {
                int increases = 0;
                int decreases = 0;
                for (int i = 1; i < aumValues.Count; i++)
                {
                    if (aumValues[i] > aumValues[i - 1]) increases++;
                    else if (aumValues[i] < aumValues[i - 1]) decreases++;
                }

                if (increases > decreases * 2) metrics.Trend = "Strong Growth";
                else if (increases > decreases) metrics.Trend = "Growing";
                else if (decreases > increases * 2) metrics.Trend = "Declining";
                else if (decreases > increases) metrics.Trend = "Shrinking";
                else metrics.Trend = "Stable";
            }
        }

        return metrics;
    }

    /// <summary>
    /// Gets growth metrics for top firms by AUM growth, useful for ranking/leaderboards.
    /// </summary>
    public List<FirmGrowthMetrics> GetTopGrowthFirms(int count = 50, string? state = null)
    {
        using var ctx = CreateContext();

        // Get the latest snapshot date
        var latestDate = ctx.Set<FirmAumHistory>()
            .OrderByDescending(h => h.SnapshotDate)
            .Select(h => h.SnapshotDate)
            .FirstOrDefault();

        if (latestDate == default) return new List<FirmGrowthMetrics>();

        // Get firms with recent snapshots
        var firmCrds = ctx.Set<FirmAumHistory>()
            .Where(h => h.SnapshotDate == latestDate && h.TotalAum > 0)
            .Select(h => h.FirmCrd)
            .Distinct()
            .ToList();

        // Apply state filter if provided
        if (!string.IsNullOrEmpty(state))
        {
            var stateFirms = ctx.Firms.AsNoTracking()
                .Where(f => f.State == state)
                .Select(f => f.CrdNumber)
                .ToHashSet();
            firmCrds = firmCrds.Where(c => stateFirms.Contains(c)).ToList();
        }

        var metrics = new List<FirmGrowthMetrics>();
        foreach (var crd in firmCrds)
        {
            var m = CalculateGrowthMetrics(crd);
            if (m != null && m.AumGrowthYoY.HasValue)
                metrics.Add(m);
        }

        return metrics
            .OrderByDescending(m => m.AumGrowthYoY)
            .Take(count)
            .ToList();
    }
}

/// <summary>
/// Computed growth metrics for a single firm.
/// </summary>
public class FirmGrowthMetrics
{
    public string FirmCrd { get; set; } = string.Empty;
    public decimal CurrentAum { get; set; }
    public DateTime LatestSnapshotDate { get; set; }
    public int SnapshotCount { get; set; }

    /// <summary>Year-over-year AUM growth percentage.</summary>
    public double? AumGrowthYoY { get; set; }
    /// <summary>AUM from approximately one year ago.</summary>
    public decimal? AumOneYearAgo { get; set; }
    /// <summary>3-year compound annual growth rate (%).</summary>
    public double? Cagr3Year { get; set; }
    /// <summary>5-year compound annual growth rate (%).</summary>
    public double? Cagr5Year { get; set; }
    /// <summary>Year-over-year client count growth (%).</summary>
    public double? ClientGrowthYoY { get; set; }
    /// <summary>Year-over-year employee count growth (%).</summary>
    public double? EmployeeGrowthYoY { get; set; }
    /// <summary>Trend: "Strong Growth", "Growing", "Stable", "Shrinking", "Declining".</summary>
    public string? Trend { get; set; }
}
