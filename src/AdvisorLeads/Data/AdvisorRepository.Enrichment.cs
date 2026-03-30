using Microsoft.EntityFrameworkCore;
using AdvisorLeads.Abstractions;
using AdvisorLeads.Models;
using System.Text;

namespace AdvisorLeads.Data;

public partial class AdvisorRepository
{
    public List<string> GetCrdsNeedingEnrichment(int limit)
    {
        using var ctx = CreateContext();
        // Include advisors that:
        //   (a) have no qualifications stored yet, OR
        //   (b) have HasDisclosures=true but no rows in the Disclosures table — the critical case
        //       where ind_exams gave them qualifications during bulk fetch but the detail
        //       endpoint was never called to retrieve the actual disclosure records.
        // Disclosure-flagged advisors sort first so the most visible gaps close earliest.
        // The Active-only restriction has been removed so inactive/terminated advisors
        // with disclosures are also enriched.
        return ctx.Advisors.AsNoTracking()
            .Where(a => a.CrdNumber != null
                && !a.IsExcluded
                && (!a.QualificationList.Any()
                    || !a.EmploymentHistory.Any()
                    || (a.HasDisclosures && !a.Disclosures.Any())))
            .Select(a => new
            {
                a.CrdNumber,
                a.UpdatedAt,
                // Compute priority once in a single SQL projection so the subquery
                // is evaluated only once (not duplicated in both WHERE and ORDER BY).
                Priority = a.HasDisclosures && !a.Disclosures.Any() ? 0 : 1
            })
            .OrderBy(a => a.Priority)
            .ThenBy(a => a.UpdatedAt)
            .Take(limit)
            .Select(a => a.CrdNumber!)
            .ToList();
    }

    /// <summary>
    /// Returns CRD numbers for advisors that need SEC IAPD enrichment:
    /// those without qualifications stored, prioritizing SEC-sourced records
    /// and those not already enriched by FINRA detail fetch.
    /// </summary>
    public List<string> GetCrdsNeedingIapdEnrichment(int limit = 200)
    {
        using var ctx = CreateContext();
        return ctx.Advisors.AsNoTracking()
            .Where(a => a.CrdNumber != null
                && !a.IsExcluded
                && !a.QualificationList.Any()
                && !a.EmploymentHistory.Any())
            .OrderBy(a => a.Source != null && a.Source.Contains("SEC") ? 0 : 1)
            .ThenBy(a => a.UpdatedAt)
            .Take(limit)
            .Select(a => a.CrdNumber!)
            .ToList();
    }

    // ── Sanction Enrichment ───────────────────────────────────────────────

    public List<Advisor> GetAdvisorsForSanctionEnrichment(int limit)
    {
        using var ctx = CreateContext();
        var cutoff = DateTime.UtcNow.AddDays(-30);
        return ctx.Advisors.AsNoTracking()
            .Where(a => a.CrdNumber != null
                && !a.IsExcluded
                && a.HasDisclosures
                && (a.SanctionEnrichedAt == null || a.SanctionEnrichedAt < cutoff))
            .OrderByDescending(a => a.IsFavorited)
            .ThenByDescending(a => a.DisclosureCount)
            .Take(limit)
            .ToList();
    }

    public List<Firm> GetFirmsForSanctionEnrichment(int limit)
    {
        using var ctx = CreateContext();
        var cutoff = DateTime.UtcNow.AddDays(-30);
        return ctx.Firms.AsNoTracking()
            .Where(f => !f.IsExcluded
                && (f.SanctionEnrichedAt == null || f.SanctionEnrichedAt < cutoff))
            .OrderBy(f => f.SanctionEnrichedAt)
            .Take(limit)
            .ToList();
    }

    // ── SEC Enforcement Actions ───────────────────────────────────────────

    public List<(string Crd, string First, string Last)> GetAdvisorsForSecEnforcementCheck(int limit)
    {
        using var ctx = CreateContext();
        var cutoff = DateTime.UtcNow.AddDays(-180);
        return ctx.Advisors.AsNoTracking()
            .Where(a => a.CrdNumber != null
                && !a.IsExcluded
                && (a.SecEnforcementEnrichedAt == null || a.SecEnforcementEnrichedAt < cutoff))
            .OrderByDescending(a => a.IsFavorited)
            .ThenByDescending(a => a.YearsOfExperience)
            .Take(limit)
            .Select(a => new { a.CrdNumber, a.FirstName, a.LastName })
            .ToList()
            .Select(a => (a.CrdNumber!, a.FirstName, a.LastName))
            .ToList();
    }

    // ── Court Records ─────────────────────────────────────────────────────

    public List<(string Crd, int Id, string First, string Last, string? State)>
        GetAdvisorsForCourtRecordCheck(int limit)
    {
        using var ctx = CreateContext();
        var cutoff = DateTime.UtcNow.AddDays(-180);
        return ctx.Advisors.AsNoTracking()
            .Where(a => a.CrdNumber != null
                && !a.IsExcluded
                && (a.CourtRecordEnrichedAt == null || a.CourtRecordEnrichedAt < cutoff))
            .OrderByDescending(a => a.IsFavorited)
            .ThenByDescending(a => a.YearsOfExperience)
            .Take(limit)
            .Select(a => new { a.CrdNumber, a.Id, a.FirstName, a.LastName, a.State })
            .ToList()
            .Select(a => (a.CrdNumber!, a.Id, a.FirstName, a.LastName, a.State))
            .ToList();
    }
}
