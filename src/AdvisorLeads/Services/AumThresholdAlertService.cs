using Microsoft.EntityFrameworkCore;
using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Checks configured AUM threshold rules against the latest FirmAumHistory snapshots.
/// Call this after AumAnalyticsService.SnapshotFirmData() so the latest snapshot is available.
/// </summary>
public class AumThresholdAlertService
{
    private readonly string _dbPath;
    private readonly AlertRepository _alertRepo;

    public AumThresholdAlertService(string dbPath, AlertRepository alertRepo)
    {
        _dbPath = dbPath;
        _alertRepo = alertRepo;
    }

    private DatabaseContext CreateContext() => new DatabaseContext(_dbPath);

    /// <summary>
    /// Scans all active FirmAumAlertRules and generates alerts where AUM has crossed
    /// the configured threshold since the rule was last checked.
    ///
    /// Algorithm per rule:
    ///   1. Load the two most recent FirmAumHistory rows for the firm.
    ///   2. If fewer than 2 rows exist, skip (need at least one baseline).
    ///   3. Compare previous vs. current total AUM against the threshold.
    ///   4. On trigger: insert AlertLog, call MarkAumRuleTriggered(), deactivate rule.
    ///
    /// Returns number of alerts generated.
    /// </summary>
    public Task<int> CheckAumThresholdsAsync(IProgress<string>? progress = null)
        => Task.FromResult(CheckThresholds(progress));

    public int CheckThresholds(IProgress<string>? progress = null)
    {
        var rules = _alertRepo.GetActiveAumRules();
        if (rules.Count == 0) return 0;

        var alerts = new List<AlertLog>();
        var now = DateTime.UtcNow;

        using var ctx = CreateContext();

        var firmCrds = rules.Select(r => r.FirmCrd).ToHashSet();
        var recentHistories = ctx.FirmAumHistory.AsNoTracking()
            .Where(h => firmCrds.Contains(h.FirmCrd))
            .OrderByDescending(h => h.SnapshotDate)
            .ToList()
            .GroupBy(h => h.FirmCrd)
            .ToDictionary(g => g.Key, g => g.Take(2).OrderBy(h => h.SnapshotDate).ToList());

        foreach (var rule in rules)
        {
            if (!recentHistories.TryGetValue(rule.FirmCrd, out var history) || history.Count < 2)
                continue;

            var prev = history[0].TotalAum ?? 0;
            var curr = history[1].TotalAum ?? 0;
            if (prev == 0 || curr == 0) continue;

            bool triggered = rule.ThresholdType == "CrossAbove"
                ? prev < rule.ThresholdAmount && curr >= rule.ThresholdAmount
                : prev > rule.ThresholdAmount && curr <= rule.ThresholdAmount;

            if (!triggered) continue;

            string direction = rule.ThresholdType == "CrossAbove" ? "crossed above" : "dropped below";
            string threshold = FormatHelpers.FormatAum(rule.ThresholdAmount);

            alerts.Add(new AlertLog
            {
                AlertType  = "AumThreshold",
                Severity   = "Medium",
                EntityType = "Firm",
                EntityCrd  = rule.FirmCrd,
                EntityName = rule.FirmName,
                Summary    = $"{rule.FirmName} AUM {direction} {threshold} "
                           + $"(was {FormatHelpers.FormatAum(prev)}, now {FormatHelpers.FormatAum(curr)})",
                Detail     = $"Rule: {rule.ThresholdType} {threshold}. "
                           + $"Comparison: {history[0].SnapshotDate:yyyy-MM} vs {history[1].SnapshotDate:yyyy-MM}.",
                OldValue   = prev.ToString("F0"),
                NewValue   = curr.ToString("F0"),
                DetectedAt = now,
                CreatedAt  = now
            });

            _alertRepo.MarkAumRuleTriggered(rule.Id, now);
        }

        if (alerts.Count > 0)
        {
            _alertRepo.AddAlerts(alerts);
            progress?.Report($"✓ AUM Thresholds: {alerts.Count} threshold crossing(s) detected.");
        }

        return alerts.Count;
    }
}
