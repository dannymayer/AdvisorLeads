using AdvisorLeads.Abstractions;
using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Orchestrates fetching data from FINRA and SEC, merging duplicates, and persisting to database.
/// </summary>
public class DataSyncService : IDataSyncService
{
    private readonly Dictionary<string, IAdvisorDataSource> _dataSources;
    private readonly IAdvisorRepository _repo;

    public DataSyncService(IEnumerable<IAdvisorDataSource> dataSources, IAdvisorRepository repo)
    {
        _dataSources = dataSources.ToDictionary(s => s.SourceTag, StringComparer.OrdinalIgnoreCase);
        _repo = repo;
    }

    /// <summary>
    /// Searches both FINRA and SEC for the given query, merges results, and persists to the database.
    /// Returns a <see cref="SyncResult"/> with counts of new and updated records.
    /// </summary>
    public async Task<SyncResult> FetchAndSyncAsync(string query, string? state = null,
        bool includeFinra = true, bool includeSec = true,
        IProgress<string>? progress = null)
    {
        var allAdvisors = new Dictionary<string, Advisor>(StringComparer.OrdinalIgnoreCase);

        if (includeFinra && _dataSources.TryGetValue("FINRA", out var finraSource))
        {
            var finraResults = await finraSource.SearchAsync(query, state, progress);
            foreach (var a in finraResults)
            {
                var key = GetMergeKey(a);
                if (!allAdvisors.ContainsKey(key))
                    allAdvisors[key] = a;
                else
                    MergeAdvisor(allAdvisors[key], a);
            }
        }

        if (includeSec && _dataSources.TryGetValue("SEC", out var secSource))
        {
            var secResults = await secSource.SearchAsync(query, state, progress);
            foreach (var a in secResults)
            {
                var key = GetMergeKey(a);
                if (!allAdvisors.ContainsKey(key))
                    allAdvisors[key] = a;
                else
                    MergeAdvisor(allAdvisors[key], a);
            }
        }

        progress?.Report($"Processing {allAdvisors.Count} unique advisors...");

        var results = new List<Advisor>();
        int newCount = 0;
        int updatedCount = 0;
        int errorCount = 0;

        foreach (var advisor in allAdvisors.Values)
        {
            // Fetch full details if we have a CRD, dispatching to the advisor's primary source
            if (!string.IsNullOrEmpty(advisor.CrdNumber))
            {
                var primaryTag = advisor.Source?.Split(',')[0] ?? "FINRA";
                if (_dataSources.TryGetValue(primaryTag, out var detailSource))
                {
                    var detail = await detailSource.GetDetailAsync(advisor.CrdNumber, progress);
                    if (detail != null)
                        MergeAdvisor(advisor, detail);
                }
            }

            try
            {
                _repo.UpsertAdvisor(advisor, out bool wasNew);
                results.Add(advisor);

                if (wasNew) newCount++;
                else updatedCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                string crd = advisor.CrdNumber ?? "unknown";
                string inner = ex.InnerException?.Message ?? ex.Message;
                progress?.Report($"⚠ Skipped CRD {crd}: {inner}");
            }
        }

        string summary = $"Sync complete. {newCount} new, {updatedCount} updated.";
        if (errorCount > 0)
            summary += $" ({errorCount} skipped due to errors)";
        progress?.Report(summary);

        _repo.ResolveAdvisorFirmLinks();
        _repo.UpdateFirmAdvisorCounts();

        return new SyncResult(results, newCount, updatedCount);
    }

    /// <summary>
    /// Fetches details for a single advisor by CRD and updates the database record.
    /// </summary>
    public async Task<Advisor?> RefreshAdvisorAsync(string crd, IProgress<string>? progress = null)
    {
        progress?.Report($"Refreshing advisor CRD #{crd}...");

        Advisor? merged = null;
        foreach (var source in _dataSources.Values)
        {
            var detail = await source.GetDetailAsync(crd, progress);
            if (detail != null)
            {
                if (merged == null) merged = detail;
                else MergeAdvisor(merged, detail);
            }
        }

        if (merged != null)
        {
            _repo.UpsertAdvisor(merged);
            _repo.ResolveAdvisorFirmLinks();
            progress?.Report($"Advisor CRD #{crd} updated successfully.");
        }

        return merged;
    }

    /// <summary>
    /// Returns a stable merge key. CRD is preferred; falls back to name + firm.
    /// </summary>
    private static string GetMergeKey(Advisor a)
    {
        if (!string.IsNullOrEmpty(a.CrdNumber))
            return $"crd:{a.CrdNumber}";
        return $"name:{a.LastName?.ToLower()}:{a.FirstName?.ToLower()}:{a.CurrentFirmCrd?.ToLower()}";
    }

    /// <summary>
    /// Merges fields from <paramref name="source"/> into <paramref name="target"/>,
    /// preferring non-null values and combining sources.
    /// </summary>
    private static void MergeAdvisor(Advisor target, Advisor source)
    {
        target.IapdNumber ??= source.IapdNumber;
        target.MiddleName ??= source.MiddleName;
        target.Title ??= source.Title;
        target.Email ??= source.Email;
        target.Phone ??= source.Phone;
        target.City ??= source.City;
        target.State ??= source.State;
        target.ZipCode ??= source.ZipCode;
        target.Licenses ??= source.Licenses;
        target.Qualifications ??= source.Qualifications;
        target.CurrentFirmName ??= source.CurrentFirmName;
        target.CurrentFirmCrd ??= source.CurrentFirmCrd;
        target.RegistrationStatus ??= source.RegistrationStatus;
        target.RegistrationDate ??= source.RegistrationDate;
        target.YearsOfExperience ??= source.YearsOfExperience;

        // Merge disclosure data (take max)
        if (source.HasDisclosures) target.HasDisclosures = true;
        if (source.DisclosureCount > target.DisclosureCount)
            target.DisclosureCount = source.DisclosureCount;

        // Merge source tags
        if (!string.IsNullOrEmpty(source.Source) && !string.IsNullOrEmpty(target.Source))
        {
            if (!target.Source!.Contains(source.Source))
                target.Source = $"{target.Source},{source.Source}";
        }
        else
        {
            target.Source ??= source.Source;
        }

        // Merge employment history (avoid duplicates by firm name)
        foreach (var emp in source.EmploymentHistory)
        {
            if (!target.EmploymentHistory.Any(e =>
                string.Equals(e.FirmName, emp.FirmName, StringComparison.OrdinalIgnoreCase)
                && e.StartDate == emp.StartDate))
            {
                target.EmploymentHistory.Add(emp);
            }
        }

        // Merge disclosures
        foreach (var disc in source.Disclosures)
        {
            if (!target.Disclosures.Any(d =>
                string.Equals(d.Type, disc.Type, StringComparison.OrdinalIgnoreCase)
                && d.Date == disc.Date))
            {
                target.Disclosures.Add(disc);
            }
        }

        // Merge qualifications
        foreach (var qual in source.QualificationList)
        {
            if (!target.QualificationList.Any(q =>
                string.Equals(q.Code ?? q.Name, qual.Code ?? qual.Name, StringComparison.OrdinalIgnoreCase)))
            {
                target.QualificationList.Add(qual);
            }
        }

        // Merge registrations
        foreach (var reg in source.Registrations)
        {
            if (!target.Registrations.Any(r =>
                string.Equals(r.StateCode, reg.StateCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.RegistrationCategory, reg.RegistrationCategory, StringComparison.OrdinalIgnoreCase)))
            {
                target.Registrations.Add(reg);
            }
        }

        target.RegAuthorities ??= source.RegAuthorities;
    }
}

/// <summary>Result returned by <see cref="DataSyncService.FetchAndSyncAsync"/>.</summary>
public record SyncResult(List<Advisor> Advisors, int NewCount, int UpdatedCount)
{
    public int Total => NewCount + UpdatedCount;
}
