using Microsoft.EntityFrameworkCore;
using AdvisorLeads.Models;

namespace AdvisorLeads.Data;

public class AlertRepository
{
    private readonly string _dbPath;

    public AlertRepository(string databasePath)
    {
        _dbPath = databasePath;
    }

    private DatabaseContext CreateContext() => new DatabaseContext(_dbPath);

    // ── AlertLog CRUD ────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a batch of alert records, skipping near-duplicates detected within the last 24 hours.
    /// </summary>
    public void AddAlerts(IEnumerable<AlertLog> alerts)
    {
        using var ctx = CreateContext();
        var cutoff = DateTime.UtcNow.AddHours(-24);

        var recentKeys = ctx.AlertLog.AsNoTracking()
            .Where(a => a.DetectedAt >= cutoff)
            .Select(a => new { a.EntityCrd, a.AlertType, a.OldValue, a.NewValue })
            .ToList()
            .Select(a => $"{a.EntityCrd}|{a.AlertType}|{a.OldValue}|{a.NewValue}")
            .ToHashSet();

        foreach (var alert in alerts)
        {
            var key = $"{alert.EntityCrd}|{alert.AlertType}|{alert.OldValue}|{alert.NewValue}";
            if (recentKeys.Contains(key)) continue;

            if (alert.DetectedAt == default) alert.DetectedAt = DateTime.UtcNow;
            if (alert.CreatedAt == default) alert.CreatedAt = DateTime.UtcNow;

            ctx.AlertLog.Add(alert);
            recentKeys.Add(key);
        }

        ctx.SaveChanges();
    }

    public void AddAlert(AlertLog alert) => AddAlerts(new[] { alert });

    /// <summary>Returns recent alerts ordered by DetectedAt descending.</summary>
    public List<AlertLog> GetRecentAlerts(int limit = 50, bool unreadOnly = false)
    {
        using var ctx = CreateContext();
        var query = ctx.AlertLog.AsNoTracking().AsQueryable();
        if (unreadOnly) query = query.Where(a => !a.IsRead);
        return query.OrderByDescending(a => a.DetectedAt).Take(limit).ToList();
    }

    /// <summary>Returns all alerts for a given entity CRD.</summary>
    public List<AlertLog> GetAlertsForEntity(string entityCrd, int limit = 100)
    {
        using var ctx = CreateContext();
        return ctx.AlertLog.AsNoTracking()
            .Where(a => a.EntityCrd == entityCrd)
            .OrderByDescending(a => a.DetectedAt)
            .Take(limit)
            .ToList();
    }

    public int GetUnreadCount()
    {
        using var ctx = CreateContext();
        return ctx.AlertLog.AsNoTracking().Count(a => !a.IsRead && !a.IsAcknowledged);
    }

    public void MarkRead(int alertId)
    {
        using var ctx = CreateContext();
        ctx.AlertLog.Where(a => a.Id == alertId)
            .ExecuteUpdate(s => s.SetProperty(a => a.IsRead, true));
    }

    /// <summary>Marks all alerts as read. Returns count updated.</summary>
    public int MarkAllRead()
    {
        using var ctx = CreateContext();
        return ctx.AlertLog.Where(a => !a.IsRead)
            .ExecuteUpdate(s => s.SetProperty(a => a.IsRead, true));
    }

    public void Acknowledge(int alertId)
    {
        using var ctx = CreateContext();
        ctx.AlertLog.Where(a => a.Id == alertId)
            .ExecuteUpdate(s => s
                .SetProperty(a => a.IsAcknowledged, true)
                .SetProperty(a => a.IsRead, true));
    }

    public void AcknowledgeAlert(int alertId) => Acknowledge(alertId);

    /// <summary>Deletes acknowledged alerts older than the cutoff. Returns count deleted.</summary>
    public int PruneOldAlerts(DateTime olderThan)
    {
        using var ctx = CreateContext();
        return ctx.AlertLog
            .Where(a => a.IsAcknowledged && a.DetectedAt < olderThan)
            .ExecuteDelete();
    }

    public void DeleteOlderThan(DateTime cutoff) => PruneOldAlerts(cutoff);

    // ── FirmAumAlertRule CRUD ────────────────────────────────────────────

    public List<FirmAumAlertRule> GetActiveAumRules()
    {
        using var ctx = CreateContext();
        return ctx.FirmAumAlertRules.AsNoTracking()
            .Where(r => r.IsActive)
            .ToList();
    }

    public FirmAumAlertRule? GetAumRuleForFirm(string firmCrd)
    {
        using var ctx = CreateContext();
        return ctx.FirmAumAlertRules.AsNoTracking()
            .FirstOrDefault(r => r.FirmCrd == firmCrd);
    }

    public void AddAumRule(FirmAumAlertRule rule) => UpsertAumRule(rule);

    public void UpdateAumRule(FirmAumAlertRule rule) => UpsertAumRule(rule);

    public void UpsertAumRule(FirmAumAlertRule rule)
    {
        using var ctx = CreateContext();
        if (rule.Id == 0)
        {
            if (rule.CreatedAt == default) rule.CreatedAt = DateTime.UtcNow;
            ctx.FirmAumAlertRules.Add(rule);
        }
        else
        {
            ctx.FirmAumAlertRules.Update(rule);
        }
        ctx.SaveChanges();
    }

    public void DeleteAumRule(int ruleId)
    {
        using var ctx = CreateContext();
        ctx.FirmAumAlertRules.Where(r => r.Id == ruleId).ExecuteDelete();
    }

    /// <summary>Updates LastTriggeredAt and deactivates the rule until manually reset.</summary>
    public void MarkAumRuleTriggered(int ruleId, DateTime triggeredAt)
    {
        using var ctx = CreateContext();
        ctx.FirmAumAlertRules.Where(r => r.Id == ruleId)
            .ExecuteUpdate(s => s
                .SetProperty(r => r.LastTriggeredAt, triggeredAt)
                .SetProperty(r => r.IsActive, false));
    }

    // ── MarketWatchRule CRUD ─────────────────────────────────────────────

    public List<MarketWatchRule> GetActiveMarketWatchRules()
    {
        using var ctx = CreateContext();
        return ctx.MarketWatchRules.AsNoTracking()
            .Where(r => r.IsActive)
            .ToList();
    }

    public void AddMarketWatchRule(MarketWatchRule rule) => UpsertMarketWatchRule(rule);

    public void UpsertMarketWatchRule(MarketWatchRule rule)
    {
        using var ctx = CreateContext();
        if (rule.Id == 0)
        {
            if (rule.CreatedAt == default) rule.CreatedAt = DateTime.UtcNow;
            rule.UpdatedAt = DateTime.UtcNow;
            ctx.MarketWatchRules.Add(rule);
        }
        else
        {
            rule.UpdatedAt = DateTime.UtcNow;
            ctx.MarketWatchRules.Update(rule);
        }
        ctx.SaveChanges();
    }

    public void DeleteMarketWatchRule(int ruleId)
    {
        using var ctx = CreateContext();
        ctx.MarketWatchRules.Where(r => r.Id == ruleId).ExecuteDelete();
    }
}
