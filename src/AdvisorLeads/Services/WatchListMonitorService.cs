using AdvisorLeads.Abstractions;
using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Monitors watched advisors for changes between sync cycles.
/// Compares freshly-fetched data against stored records and writes
/// new AlertLog entries for any detected changes.
/// </summary>
public class WatchListMonitorService
{
    private readonly IAdvisorRepository _repo;
    private readonly AlertRepository _alertRepo;

    public WatchListMonitorService(IAdvisorRepository repo, AlertRepository alertRepo)
    {
        _repo = repo;
        _alertRepo = alertRepo;
    }

    /// <summary>
    /// Compares a freshly-fetched advisor against the stored record and returns
    /// a list of AlertLog entries representing detected changes.
    ///
    /// Checks in order of decreasing priority:
    ///   1. Disclosure count increase
    ///   2. Current firm CRD changed
    ///   3. Registration status changed
    ///   4. BcScope or IaScope changed to "Inactive"
    ///   5. License string changed
    /// </summary>
    public static List<AlertLog> CheckAdvisorForChanges(Advisor existing, Advisor incoming)
    {
        var alerts = new List<AlertLog>();
        var now = DateTime.UtcNow;

        // ── Disclosure count increase ──────────────────────────────────
        int existingCount = existing.DisclosureCount;
        int freshCount = incoming.DisclosureCount;

        if (freshCount > existingCount)
        {
            var newTypes = new List<string>();
            if (incoming.HasCriminalDisclosure && !existing.HasCriminalDisclosure)
                newTypes.Add("Criminal");
            if (incoming.HasRegulatoryDisclosure && !existing.HasRegulatoryDisclosure)
                newTypes.Add("Regulatory");
            if (incoming.HasCivilDisclosure && !existing.HasCivilDisclosure)
                newTypes.Add("Civil");
            if (incoming.HasCustomerComplaintDisclosure && !existing.HasCustomerComplaintDisclosure)
                newTypes.Add("Customer Complaint");
            if (incoming.HasFinancialDisclosure && !existing.HasFinancialDisclosure)
                newTypes.Add("Financial");
            if (incoming.HasTerminationDisclosure && !existing.HasTerminationDisclosure)
                newTypes.Add("Termination");

            int added = freshCount - existingCount;
            string typeDesc = newTypes.Count > 0
                ? $" ({string.Join(", ", newTypes)})"
                : string.Empty;

            alerts.Add(new AlertLog
            {
                AlertType  = "Disclosure",
                Severity   = "High",
                EntityType = "Advisor",
                EntityCrd  = existing.CrdNumber ?? string.Empty,
                EntityName = existing.FullName,
                Summary    = $"{existing.FullName} at {existing.CurrentFirmName ?? "unknown firm"} — "
                           + $"{added} new disclosure{(added != 1 ? "s" : string.Empty)} added{typeDesc}",
                Detail     = $"Previous count: {existingCount}. New count: {freshCount}.",
                OldValue   = existingCount.ToString(),
                NewValue   = freshCount.ToString(),
                SourceUrl  = existing.BrokerCheckUrl,
                DetectedAt = now,
                CreatedAt  = now
            });
        }

        // ── Firm change ────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(incoming.CurrentFirmCrd)
            && incoming.CurrentFirmCrd != existing.CurrentFirmCrd)
        {
            bool isDeparture = string.IsNullOrEmpty(incoming.CurrentFirmName);
            alerts.Add(new AlertLog
            {
                AlertType  = "FirmChange",
                Severity   = isDeparture ? "High" : "Medium",
                EntityType = "Advisor",
                EntityCrd  = existing.CrdNumber ?? string.Empty,
                EntityName = existing.FullName,
                Summary    = isDeparture
                    ? $"{existing.FullName} has left {existing.CurrentFirmName ?? existing.CurrentFirmCrd} (no new firm registered)"
                    : $"{existing.FullName} moved from {existing.CurrentFirmName ?? existing.CurrentFirmCrd} to {incoming.CurrentFirmName ?? incoming.CurrentFirmCrd}",
                OldValue   = existing.CurrentFirmCrd,
                NewValue   = incoming.CurrentFirmCrd,
                SourceUrl  = existing.BrokerCheckUrl,
                DetectedAt = now,
                CreatedAt  = now
            });
        }

        // ── Registration status change ─────────────────────────────────
        if (!string.IsNullOrEmpty(incoming.RegistrationStatus)
            && incoming.RegistrationStatus != existing.RegistrationStatus)
        {
            alerts.Add(new AlertLog
            {
                AlertType  = "StatusChange",
                Severity   = "Medium",
                EntityType = "Advisor",
                EntityCrd  = existing.CrdNumber ?? string.Empty,
                EntityName = existing.FullName,
                Summary    = $"{existing.FullName} — registration status changed from '{existing.RegistrationStatus}' to '{incoming.RegistrationStatus}'",
                OldValue   = existing.RegistrationStatus,
                NewValue   = incoming.RegistrationStatus,
                SourceUrl  = existing.BrokerCheckUrl,
                DetectedAt = now,
                CreatedAt  = now
            });
        }

        // ── Scope change (active → inactive) ──────────────────────────
        bool bcWentInactive = !string.IsNullOrEmpty(incoming.BcScope)
            && incoming.BcScope != existing.BcScope
            && incoming.BcScope.Contains("Inactive", StringComparison.OrdinalIgnoreCase);
        bool iaWentInactive = !string.IsNullOrEmpty(incoming.IaScope)
            && incoming.IaScope != existing.IaScope
            && incoming.IaScope.Contains("Inactive", StringComparison.OrdinalIgnoreCase);

        if (bcWentInactive || iaWentInactive)
        {
            string scope = bcWentInactive ? "BrokerCheck" : "IAPD";
            alerts.Add(new AlertLog
            {
                AlertType  = "StatusChange",
                Severity   = "High",
                EntityType = "Advisor",
                EntityCrd  = existing.CrdNumber ?? string.Empty,
                EntityName = existing.FullName,
                Summary    = $"{existing.FullName} — {scope} registration scope went inactive",
                OldValue   = bcWentInactive ? existing.BcScope : existing.IaScope,
                NewValue   = bcWentInactive ? incoming.BcScope : incoming.IaScope,
                SourceUrl  = existing.BrokerCheckUrl,
                DetectedAt = now,
                CreatedAt  = now
            });
        }

        // ── License change ─────────────────────────────────────────────
        if (!string.IsNullOrEmpty(incoming.Licenses)
            && incoming.Licenses != existing.Licenses)
        {
            alerts.Add(new AlertLog
            {
                AlertType  = "LicenseChange",
                Severity   = "Low",
                EntityType = "Advisor",
                EntityCrd  = existing.CrdNumber ?? string.Empty,
                EntityName = existing.FullName,
                Summary    = $"{existing.FullName} — license/exam profile changed",
                OldValue   = existing.Licenses,
                NewValue   = incoming.Licenses,
                SourceUrl  = existing.BrokerCheckUrl,
                DetectedAt = now,
                CreatedAt  = now
            });
        }

        return alerts;
    }

    /// <summary>
    /// Detects changes between the existing and incoming advisor records and persists
    /// any resulting alerts. Called from UpsertAdvisor when IsWatched = true.
    /// </summary>
    public void RecordNewAdvisorAlerts(Advisor existing, Advisor incoming)
    {
        var alerts = CheckAdvisorForChanges(existing, incoming);
        if (alerts.Count > 0)
            _alertRepo.AddAlerts(alerts);
    }
}
