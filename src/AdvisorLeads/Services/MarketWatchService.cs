using Microsoft.EntityFrameworkCore;
using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// After each sync cycle, identifies newly-inserted advisor records that match
/// user-defined market watch rules and generates "NewRegistration" alerts.
/// </summary>
public class MarketWatchService
{
    private readonly string _dbPath;
    private readonly AlertRepository _alertRepo;

    public MarketWatchService(string dbPath, AlertRepository alertRepo)
    {
        _dbPath = dbPath;
        _alertRepo = alertRepo;
    }

    private DatabaseContext CreateContext() => new DatabaseContext(_dbPath);

    /// <summary>
    /// Queries for advisors first seen since <paramref name="since"/> that match
    /// any active MarketWatchRule. Generates one alert per matching advisor.
    ///
    /// Deduplication: skips advisors for which a NewRegistration alert already exists
    /// in AlertLog (prevents re-alerting on subsequent sync cycles).
    ///
    /// Returns count of new alerts generated.
    /// </summary>
    public Task<int> CheckNewRegistrantsAsync(IProgress<string>? progress = null)
        => Task.FromResult(CheckNewRegistrations(DateTime.UtcNow.AddHours(-25), progress));

    public int CheckNewRegistrations(DateTime since, IProgress<string>? progress = null)
    {
        var rules = _alertRepo.GetActiveMarketWatchRules();
        if (rules.Count == 0) return 0;

        using var ctx = CreateContext();
        var now = DateTime.UtcNow;

        var newAdvisors = ctx.Advisors.AsNoTracking()
            .Where(a => !a.IsExcluded
                     && a.FirstSeenAt.HasValue
                     && a.FirstSeenAt >= since)
            .ToList();

        if (newAdvisors.Count == 0) return 0;

        var alreadyAlerted = ctx.AlertLog.AsNoTracking()
            .Where(al => al.AlertType == "NewRegistration"
                      && al.DetectedAt >= since.AddDays(-7))
            .Select(al => al.EntityCrd)
            .ToHashSet();

        var alerts = new List<AlertLog>();

        foreach (var rule in rules)
        {
            foreach (var advisor in newAdvisors)
            {
                if (alreadyAlerted.Contains(advisor.CrdNumber ?? string.Empty)) continue;

                if (!string.IsNullOrEmpty(rule.State)
                    && advisor.State != rule.State) continue;
                if (!string.IsNullOrEmpty(rule.RecordType)
                    && advisor.RecordType != rule.RecordType) continue;
                if (!string.IsNullOrEmpty(rule.LicenseContains)
                    && !(advisor.Licenses?.Contains(rule.LicenseContains,
                        StringComparison.OrdinalIgnoreCase) ?? false)) continue;
                if (rule.MinYearsExperience.HasValue
                    && advisor.YearsOfExperience.HasValue
                    && advisor.YearsOfExperience.Value < rule.MinYearsExperience.Value) continue;

                int yearsExp = advisor.YearsOfExperience ?? 0;
                string expLabel = yearsExp > 0 ? $"{yearsExp} yrs exp" : "experience unknown";

                alerts.Add(new AlertLog
                {
                    AlertType  = "NewRegistration",
                    Severity   = yearsExp >= 10 ? "Medium" : "Low",
                    EntityType = "Advisor",
                    EntityCrd  = advisor.CrdNumber ?? string.Empty,
                    EntityName = advisor.FullName,
                    Summary    = $"New registrant in {advisor.State}: {advisor.FullName} "
                               + $"at {advisor.CurrentFirmName ?? "unknown firm"} ({expLabel})"
                               + $" — matched rule '{rule.RuleName}'",
                    Detail     = $"Rule: {rule.RuleName} | State: {rule.State ?? "Any"} "
                               + $"| Type: {rule.RecordType ?? "Any"} "
                               + $"| Min Exp: {rule.MinYearsExperience?.ToString() ?? "None"}",
                    SourceUrl  = advisor.BrokerCheckUrl,
                    DetectedAt = now,
                    CreatedAt  = now
                });

                // Prevent duplicate across multiple rules for same advisor
                alreadyAlerted.Add(advisor.CrdNumber ?? string.Empty);
            }
        }

        if (alerts.Count > 0)
        {
            _alertRepo.AddAlerts(alerts);
            progress?.Report($"✓ Market Watch: {alerts.Count} new registrant(s) matched your rules.");
        }

        return alerts.Count;
    }
}
