using Microsoft.EntityFrameworkCore;
using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Detects changes in Broker Protocol membership between sync cycles.
/// Requires the before-state snapshot to be captured before UpdateBrokerProtocolStatus runs.
/// </summary>
public class BrokerProtocolAlertService
{
    private readonly string _dbPath;
    private readonly AlertRepository _alertRepo;

    public BrokerProtocolAlertService(string dbPath, AlertRepository alertRepo)
    {
        _dbPath = dbPath;
        _alertRepo = alertRepo;
    }

    private DatabaseContext CreateContext() => new DatabaseContext(_dbPath);

    /// <summary>
    /// Compares previous vs. current BP member CRD sets and generates alerts.
    ///
    /// - Firms in currentCrds but NOT in previousCrds: joined the Protocol
    /// - Firms in previousCrds but NOT in currentCrds: withdrew from Protocol
    ///
    /// Both directions are Severity="High". Withdraw is especially urgent.
    /// Returns number of alerts generated.
    /// </summary>
    public int DetectAndRecordChanges(
        HashSet<string> previousMemberCrds,
        HashSet<string> currentMemberCrds,
        IProgress<string>? progress = null)
    {
        var joined   = currentMemberCrds.Except(previousMemberCrds).ToList();
        var withdrew = previousMemberCrds.Except(currentMemberCrds).ToList();

        if (joined.Count == 0 && withdrew.Count == 0) return 0;

        var alerts = new List<AlertLog>();
        var now = DateTime.UtcNow;

        using var ctx = CreateContext();
        var allCrds = joined.Concat(withdrew).ToList();
        var firmNames = ctx.Firms.AsNoTracking()
            .Where(f => allCrds.Contains(f.CrdNumber))
            .Select(f => new { f.CrdNumber, f.Name })
            .ToDictionary(f => f.CrdNumber, f => f.Name);

        foreach (var crd in withdrew)
        {
            var name = firmNames.GetValueOrDefault(crd, crd);
            alerts.Add(new AlertLog
            {
                AlertType  = "BrokerProtocol",
                Severity   = "High",
                EntityType = "Firm",
                EntityCrd  = crd,
                EntityName = name,
                Summary    = $"\U0001f6a8 {name} WITHDREW from Broker Protocol — advisors may have limited window to move",
                Detail     = $"Firm CRD {crd} was removed from the Broker Protocol member list as of {now:yyyy-MM-dd}. "
                           + $"Advisors at this firm may lose the ability to freely move their book. "
                           + $"Consider contacting advisors at this firm immediately.",
                OldValue   = "Member",
                NewValue   = "Non-Member",
                DetectedAt = now,
                CreatedAt  = now
            });
        }

        foreach (var crd in joined)
        {
            var name = firmNames.GetValueOrDefault(crd, crd);
            alerts.Add(new AlertLog
            {
                AlertType  = "BrokerProtocol",
                Severity   = "Medium",
                EntityType = "Firm",
                EntityCrd  = crd,
                EntityName = name,
                Summary    = $"\u2705 {name} JOINED the Broker Protocol — firm is growth-mode and advisor-friendly",
                Detail     = $"Firm CRD {crd} joined the Broker Protocol as of {now:yyyy-MM-dd}. "
                           + $"Protocol membership signals the firm is open to bringing in advisors with their books.",
                OldValue   = "Non-Member",
                NewValue   = "Member",
                DetectedAt = now,
                CreatedAt  = now
            });
        }

        _alertRepo.AddAlerts(alerts);
        progress?.Report($"✓ Broker Protocol: {withdrew.Count} withdrawal(s), {joined.Count} join(s) detected.");
        return alerts.Count;
    }

    /// <summary>
    /// Wrapper that accepts Dictionary&lt;string, bool&gt; maps (CRD → was member).
    /// </summary>
    public void CheckBrokerProtocolChanges(
        Dictionary<string, bool> previousStatus,
        Dictionary<string, bool> currentStatus)
    {
        var prev = previousStatus.Where(kv => kv.Value).Select(kv => kv.Key).ToHashSet();
        var curr = currentStatus.Where(kv => kv.Value).Select(kv => kv.Key).ToHashSet();
        DetectAndRecordChanges(prev, curr);
    }
}
