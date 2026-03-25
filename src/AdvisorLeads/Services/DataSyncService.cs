using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Orchestrates fetching data from FINRA and SEC, merging duplicates, and persisting to database.
/// </summary>
public class DataSyncService
{
    private readonly FinraService _finra;
    private readonly SecIapdService _sec;
    private readonly AdvisorRepository _repo;

    public DataSyncService(FinraService finra, SecIapdService sec, AdvisorRepository repo)
    {
        _finra = finra;
        _sec = sec;
        _repo = repo;
    }

    /// <summary>
    /// Searches both FINRA and SEC for the given query, merges results, and persists to the database.
    /// Returns the list of merged/upserted advisors.
    /// </summary>
    public async Task<List<Advisor>> FetchAndSyncAsync(string query, string? state = null,
        bool includeFinra = true, bool includeSec = true,
        IProgress<string>? progress = null)
    {
        var allAdvisors = new Dictionary<string, Advisor>(StringComparer.OrdinalIgnoreCase);

        // Fetch from FINRA
        if (includeFinra)
        {
            var finraResults = await _finra.SearchAdvisorsAsync(query, state, progress: progress);
            foreach (var a in finraResults)
            {
                var key = GetMergeKey(a);
                if (!allAdvisors.ContainsKey(key))
                    allAdvisors[key] = a;
                else
                    MergeAdvisor(allAdvisors[key], a);
            }
        }

        // Fetch from SEC
        if (includeSec)
        {
            var secResults = await _sec.SearchAdvisorsAsync(query, state, progress: progress);
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
        foreach (var advisor in allAdvisors.Values)
        {
            // Fetch full details if we have a CRD
            if (!string.IsNullOrEmpty(advisor.CrdNumber))
            {
                Advisor? detail = null;
                if (advisor.Source == "FINRA" || advisor.Source == "FINRA,SEC")
                    detail = await _finra.GetAdvisorDetailAsync(advisor.CrdNumber, progress);
                else if (advisor.Source == "SEC")
                    detail = await _sec.GetAdvisorDetailAsync(advisor.CrdNumber, progress);

                if (detail != null)
                {
                    MergeAdvisor(advisor, detail);
                }
            }

            // Upsert into database
            _repo.UpsertAdvisor(advisor);
            results.Add(advisor);
        }

        progress?.Report($"Sync complete. {results.Count} advisors updated.");
        return results;
    }

    /// <summary>
    /// Fetches details for a single advisor by CRD and updates the database record.
    /// </summary>
    public async Task<Advisor?> RefreshAdvisorAsync(string crd, IProgress<string>? progress = null)
    {
        progress?.Report($"Refreshing advisor CRD #{crd}...");

        Advisor? finraDetail = await _finra.GetAdvisorDetailAsync(crd, progress);
        Advisor? secDetail = await _sec.GetAdvisorDetailAsync(crd, progress);

        Advisor? merged = finraDetail;
        if (merged == null)
            merged = secDetail;
        else if (secDetail != null)
            MergeAdvisor(merged, secDetail);

        if (merged != null)
        {
            _repo.UpsertAdvisor(merged);
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
    }
}
