using Microsoft.EntityFrameworkCore;
using AdvisorLeads.Abstractions;
using AdvisorLeads.Models;
using System.Text;

namespace AdvisorLeads.Data;

public partial class AdvisorRepository
{
    // ── Registration Level Classification ─────────────────────────────────

    /// <summary>
    /// Batch-classifies advisors into registration level categories based on source,
    /// scope fields, and registration status. Runs directly in the DB via ExecuteUpdate.
    /// Labels: "Federal" (SEC-registered), "State" (FINRA-only), "Dual" (both active),
    /// "ExemptReporting", or "State" as the fallback.
    /// </summary>
    public void ClassifyRegistrationLevels()
    {
        using var ctx = CreateContext();

        // SEC-registered IARs → Federal
        ctx.Advisors
            .Where(a => a.Source != null && a.Source.Contains("SEC") && a.IaScope == "Active")
            .ExecuteUpdate(s => s.SetProperty(a => a.RegistrationLevel, "Federal"));

        // FINRA-only (BC active, no IA) → State
        ctx.Advisors
            .Where(a => a.BcScope == "Active"
                && (a.IaScope == null || a.IaScope != "Active")
                && (a.Source == null || !a.Source.Contains("SEC")))
            .ExecuteUpdate(s => s.SetProperty(a => a.RegistrationLevel, "State"));

        // Dual (both scopes active)
        ctx.Advisors
            .Where(a => a.BcScope == "Active" && a.IaScope == "Active")
            .ExecuteUpdate(s => s.SetProperty(a => a.RegistrationLevel, "Dual"));

        // Exempt reporting
        ctx.Advisors
            .Where(a => a.RegistrationStatus != null
                && a.RegistrationStatus.Contains("Exempt")
                && a.RegistrationLevel == null)
            .ExecuteUpdate(s => s.SetProperty(a => a.RegistrationLevel, "ExemptReporting"));

        // Default remaining unclassified
        ctx.Advisors
            .Where(a => a.RegistrationLevel == null)
            .ExecuteUpdate(s => s.SetProperty(a => a.RegistrationLevel, "State"));
    }

    /// <summary>
    /// Resolves <see cref="Advisor.CurrentFirmId"/> for advisors whose
    /// <see cref="Advisor.CurrentFirmCrd"/> matches a firm in the Firms table.
    /// Uses TRIM and integer cast comparisons to handle format mismatches (e.g.
    /// leading zeros, whitespace). Re-resolves all advisors on every call so that
    /// previously wrong links are corrected.
    /// Run this after bulk upserts so the FK is kept in sync.
    /// </summary>
    public void ResolveAdvisorFirmLinks()
    {
        using var ctx = CreateContext();
        ctx.Database.ExecuteSqlRaw(@"
            UPDATE Advisors
            SET CurrentFirmId = (
                SELECT Id FROM Firms
                WHERE TRIM(CAST(CrdNumber AS TEXT)) = TRIM(CAST(Advisors.CurrentFirmCrd AS TEXT))
                   OR CAST(CrdNumber AS INTEGER) = CAST(Advisors.CurrentFirmCrd AS INTEGER)
                LIMIT 1
            )
            WHERE CurrentFirmCrd IS NOT NULL
              AND CurrentFirmCrd != ''");
    }

    /// <summary>
    /// Updates <see cref="Firm.NumberOfAdvisors"/> with the live count of non-excluded
    /// advisors in the local database whose <see cref="Advisor.CurrentFirmCrd"/> matches
    /// each firm's <see cref="Firm.CrdNumber"/>. Call this after
    /// <see cref="ResolveAdvisorFirmLinks"/> to keep the count accurate.
    /// </summary>
    /// <returns>The number of firm rows updated.</returns>
    public int UpdateFirmAdvisorCounts()
    {
        using var ctx = CreateContext();
        return ctx.Database.ExecuteSqlRaw(@"
            UPDATE Firms
            SET NumberOfAdvisors = (
                SELECT COUNT(*) FROM Advisors
                WHERE Advisors.CurrentFirmCrd = Firms.CrdNumber
                  AND Advisors.IsExcluded = 0
            )
            WHERE CrdNumber IS NOT NULL AND CrdNumber != ''");
    }
}
