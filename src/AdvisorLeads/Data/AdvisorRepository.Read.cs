using Microsoft.EntityFrameworkCore;
using AdvisorLeads.Abstractions;
using AdvisorLeads.Models;
using System.Text;

namespace AdvisorLeads.Data;

public partial class AdvisorRepository
{
    public List<Advisor> GetAdvisors(SearchFilter filter)
    {
        using var ctx = CreateContext();
        var query = ApplyAdvisorFilters(ctx.Advisors.AsNoTracking(), filter);
        return ApplyAdvisorSort(query, filter)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();
    }

    public (List<Advisor> Advisors, int TotalCount) GetAdvisorsWithCount(SearchFilter filter)
    {
        using var ctx = CreateContext();
        var query = ApplyAdvisorFilters(ctx.Advisors.AsNoTracking(), filter);
        int total = query.Count();
        var results = ApplyAdvisorSort(query, filter)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();
        return (results, total);
    }

    private static IQueryable<Advisor> ApplyAdvisorFilters(IQueryable<Advisor> query, SearchFilter filter)
    {
        if (!filter.IncludeExcluded)
            query = query.Where(a => !a.IsExcluded);

        if (!string.IsNullOrWhiteSpace(filter.NameQuery))
        {
            var term = filter.NameQuery.Trim();
            if (term.Length >= 3 && term.All(char.IsDigit))
            {
                query = query.Where(a => a.CrdNumber == term);
            }
            else
            {
                var pattern = $"%{term}%";
                query = query.Where(a =>
                    EF.Functions.Like(a.FirstName, pattern) ||
                    EF.Functions.Like(a.LastName, pattern));
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.State))
            query = query.Where(a => a.State == filter.State);

        if (!string.IsNullOrWhiteSpace(filter.FirmName))
        {
            var pattern = $"%{filter.FirmName}%";
            query = query.Where(a => EF.Functions.Like(a.CurrentFirmName!, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.FirmCrd))
            query = query.Where(a => a.CurrentFirmCrd == filter.FirmCrd);

        if (!string.IsNullOrWhiteSpace(filter.CrdNumber))
            query = query.Where(a => a.CrdNumber == filter.CrdNumber);

        if (!string.IsNullOrWhiteSpace(filter.RegistrationStatus))
            query = query.Where(a => EF.Functions.Like(a.RegistrationStatus!, filter.RegistrationStatus));

        if (!string.IsNullOrWhiteSpace(filter.LicenseType))
        {
            var pattern = $"%{filter.LicenseType}%";
            query = query.Where(a => EF.Functions.Like(a.Licenses!, pattern));
        }

        if (filter.HasDisclosures.HasValue)
            query = query.Where(a => a.HasDisclosures == filter.HasDisclosures.Value);

        if (filter.IsImportedToCrm.HasValue)
            query = query.Where(a => a.IsImportedToCrm == filter.IsImportedToCrm.Value);

        if (!string.IsNullOrWhiteSpace(filter.Source))
        {
            if (filter.Source.Equals("Both", StringComparison.OrdinalIgnoreCase)
                || filter.Source.Contains(','))
            {
                query = query.Where(a => a.Source != null && a.Source.Contains("FINRA") && a.Source.Contains("SEC"));
            }
            else
            {
                var pattern = $"%{filter.Source}%";
                query = query.Where(a => EF.Functions.Like(a.Source!, pattern));
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.RecordType))
            query = query.Where(a => a.RecordType == filter.RecordType);

        // NULL YearsOfExperience means the value is unknown; include those records
        // so they aren't silently hidden when the user applies an experience range filter.
        if (filter.MinYearsExperience.HasValue)
            query = query.Where(a => a.YearsOfExperience == null || a.YearsOfExperience >= filter.MinYearsExperience.Value);

        if (filter.MaxYearsExperience.HasValue)
            query = query.Where(a => a.YearsOfExperience == null || a.YearsOfExperience <= filter.MaxYearsExperience.Value);

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            var pattern = $"%{filter.City}%";
            query = query.Where(a => EF.Functions.Like(a.City!, pattern));
        }

        if (filter.MinDisclosureCount.HasValue && filter.MinDisclosureCount.Value > 0)
            query = query.Where(a => a.DisclosureCount >= filter.MinDisclosureCount.Value);

        if (filter.ShowFavoritesOnly)
            query = query.Where(a => a.IsFavorited);

        if (!string.IsNullOrWhiteSpace(filter.DisclosureType))
        {
            query = filter.DisclosureType switch
            {
                "Criminal" => query.Where(a => a.HasCriminalDisclosure),
                "Regulatory" => query.Where(a => a.HasRegulatoryDisclosure),
                "Civil" => query.Where(a => a.HasCivilDisclosure),
                "Customer Complaint" => query.Where(a => a.HasCustomerComplaintDisclosure),
                "Financial" => query.Where(a => a.HasFinancialDisclosure),
                "Termination" => query.Where(a => a.HasTerminationDisclosure),
                _ => query
            };
        }

        if (filter.HasActiveSanction == true)
            query = query.Where(a => a.HasActiveSanction);
        if (filter.HasSecEnforcementAction == true)
            query = query.Where(a => a.HasSecEnforcementAction);
        if (filter.HasRecentFirmChange == true)
            query = query.Where(a => a.HasRecentFirmChange);
        if (filter.HasCourtRecord == true)
            query = query.Where(a => a.CourtRecordFlag);
        if (!string.IsNullOrWhiteSpace(filter.RegistrationLevel))
            query = query.Where(a => a.RegistrationLevel == filter.RegistrationLevel);

        return query;
    }

    private static IOrderedQueryable<Advisor> ApplyAdvisorSort(IQueryable<Advisor> query, SearchFilter filter)
    {
        return filter.SortBy switch
        {
            "FirstName" => filter.SortDescending ? query.OrderByDescending(a => a.FirstName) : query.OrderBy(a => a.FirstName),
            "State" => filter.SortDescending ? query.OrderByDescending(a => a.State) : query.OrderBy(a => a.State),
            "CurrentFirmName" => filter.SortDescending ? query.OrderByDescending(a => a.CurrentFirmName) : query.OrderBy(a => a.CurrentFirmName),
            "RegistrationStatus" => filter.SortDescending ? query.OrderByDescending(a => a.RegistrationStatus) : query.OrderBy(a => a.RegistrationStatus),
            "RecordType" => filter.SortDescending ? query.OrderByDescending(a => a.RecordType) : query.OrderBy(a => a.RecordType),
            "YearsOfExperience" => filter.SortDescending ? query.OrderByDescending(a => a.YearsOfExperience) : query.OrderBy(a => a.YearsOfExperience),
            "DisclosureCount" => filter.SortDescending ? query.OrderByDescending(a => a.DisclosureCount) : query.OrderBy(a => a.DisclosureCount),
            "UpdatedAt" => filter.SortDescending ? query.OrderByDescending(a => a.UpdatedAt) : query.OrderBy(a => a.UpdatedAt),
            _ => filter.SortDescending ? query.OrderByDescending(a => a.LastName) : query.OrderBy(a => a.LastName),
        };
    }

    public Advisor? GetAdvisorById(int id)
    {
        using var ctx = CreateContext();
        return ctx.Advisors.AsNoTracking()
            .Include(a => a.EmploymentHistory)
            .Include(a => a.Disclosures)
            .Include(a => a.QualificationList)
            .Include(a => a.Registrations)
            .FirstOrDefault(a => a.Id == id);
    }

    public Advisor? GetAdvisorByCrd(string crd)
    {
        using var ctx = CreateContext();
        var stub = ctx.Advisors.AsNoTracking()
            .Where(a => a.CrdNumber == crd)
            .Select(a => new { a.Id })
            .FirstOrDefault();
        if (stub == null) return null;
        return GetAdvisorById(stub.Id);
    }

    public List<Advisor> GetWatchedAdvisors()
    {
        using var ctx = CreateContext();
        return ctx.Advisors.AsNoTracking()
            .Where(a => a.IsWatched && !a.IsExcluded)
            .OrderBy(a => a.LastName)
            .ToList();
    }

    public List<string> GetWatchedAdvisorCrds()
    {
        using var ctx = CreateContext();
        return ctx.Advisors.AsNoTracking()
            .Where(a => a.IsWatched && a.CrdNumber != null)
            .OrderBy(a => a.LastName)
            .Select(a => a.CrdNumber!)
            .ToList();
    }

    public int GetWatchedCount()
    {
        using var ctx = CreateContext();
        return ctx.Advisors.AsNoTracking().Count(a => a.IsWatched && !a.IsExcluded);
    }

    public List<Firm> GetWatchedFirms()
    {
        using var ctx = CreateContext();
        return ctx.Firms.AsNoTracking()
            .Where(f => f.IsWatched && !f.IsExcluded)
            .OrderBy(f => f.Name)
            .ToList();
    }

    /// <summary>
    /// Returns a HashSet of CRD numbers for all firms currently marked BrokerProtocolMember = true.
    /// Used by BrokerProtocolAlertService to capture before-state snapshot.
    /// </summary>
    public HashSet<string> GetBrokerProtocolMemberCrds()
    {
        using var ctx = CreateContext();
        return ctx.Firms.AsNoTracking()
            .Where(f => f.BrokerProtocolMember && f.CrdNumber != null)
            .Select(f => f.CrdNumber!)
            .ToHashSet();
    }

    // ── Firms ─────────────────────────────────────────────────────────────

    public List<Firm> GetFirms(FirmSearchFilter? filter = null)
    {
        using var ctx = CreateContext();
        filter ??= new FirmSearchFilter();
        var query = ApplyFirmFilters(ctx.Firms.AsNoTracking(), filter);
        var ordered = ApplyFirmSort(query, filter);
        if (filter.PageSize > 0)
            return ordered
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();
        return ordered.ToList();
    }

    public (List<Firm> Firms, int TotalCount) GetFirmsWithCount(FirmSearchFilter? filter = null)
    {
        using var ctx = CreateContext();
        filter ??= new FirmSearchFilter();
        var query = ApplyFirmFilters(ctx.Firms.AsNoTracking(), filter);
        int total = query.Count();
        var results = ApplyFirmSort(query, filter)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();
        return (results, total);
    }

    private static IQueryable<Firm> ApplyFirmFilters(IQueryable<Firm> query, FirmSearchFilter filter)
    {
        query = query.Where(f => !f.IsExcluded);

        if (!string.IsNullOrWhiteSpace(filter.NameQuery))
        {
            var pattern = $"%{filter.NameQuery}%";
            query = query.Where(f => EF.Functions.Like(f.Name, pattern));
        }
        if (!string.IsNullOrWhiteSpace(filter.State))
            query = query.Where(f => f.State == filter.State);
        if (!string.IsNullOrWhiteSpace(filter.RecordType))
            query = query.Where(f => f.RecordType == filter.RecordType);
        if (!string.IsNullOrWhiteSpace(filter.RegistrationStatus))
        {
            var pattern = $"%{filter.RegistrationStatus}%";
            query = query.Where(f => EF.Functions.Like(f.RegistrationStatus!, pattern));
        }
        if (filter.MinAdvisors.HasValue && filter.MinAdvisors.Value > 0)
            query = query.Where(f => f.NumberOfAdvisors >= filter.MinAdvisors.Value);
        if (filter.BrokerProtocolOnly)
            query = query.Where(f => f.BrokerProtocolMember);
        if (filter.MinRegulatoryAum.HasValue)
            query = query.Where(f => f.RegulatoryAum >= filter.MinRegulatoryAum.Value);
        if (filter.HasCustody.HasValue)
            query = query.Where(f => f.HasCustody == filter.HasCustody.Value);
        if (filter.HasDiscretionaryAuthority.HasValue)
            query = query.Where(f => f.HasDiscretionaryAuthority == filter.HasDiscretionaryAuthority.Value);
        if (!string.IsNullOrWhiteSpace(filter.CompensationType))
        {
            if (filter.CompensationType == "Fee-Only")
                query = query.Where(f => f.CompensationFeeOnly == true);
            else if (filter.CompensationType == "Commission")
                query = query.Where(f => f.CompensationCommission == true);
            else if (filter.CompensationType == "Both")
                query = query.Where(f => f.CompensationFeeOnly == true && f.CompensationCommission == true);
        }
        if (filter.MinPrivateFunds.HasValue && filter.MinPrivateFunds.Value > 0)
            query = query.Where(f => f.PrivateFundCount >= filter.MinPrivateFunds.Value);

        if (!string.IsNullOrWhiteSpace(filter.InvestmentStrategy))
        {
            var pat = $"%{filter.InvestmentStrategy}%";
            query = query.Where(f => EF.Functions.Like(f.InvestmentStrategies!, pat));
        }
        if (!string.IsNullOrWhiteSpace(filter.OwnershipStructure))
            query = query.Where(f => f.OwnershipStructure == filter.OwnershipStructure);
        if (filter.HasActiveSanction == true)
            query = query.Where(f => f.HasActiveSanction);
        if (filter.HasSecEnforcementAction == true)
            query = query.Where(f => f.HasSecEnforcementAction);
        if (!string.IsNullOrWhiteSpace(filter.RegistrationLevel))
            query = query.Where(f => f.RegistrationLevel == filter.RegistrationLevel);
        if (filter.CryptoExposure == true)
            query = query.Where(f => f.CryptoExposure == true);
        if (filter.WrapFeePrograms == true)
            query = query.Where(f => f.WrapFeePrograms == true);

        return query;
    }

    private static IOrderedQueryable<Firm> ApplyFirmSort(IQueryable<Firm> query, FirmSearchFilter filter)
    {
        return filter.SortBy switch
        {
            "State" => filter.SortDescending ? query.OrderByDescending(f => f.State) : query.OrderBy(f => f.State),
            "NumberOfAdvisors" => filter.SortDescending ? query.OrderByDescending(f => f.NumberOfAdvisors) : query.OrderBy(f => f.NumberOfAdvisors),
            "RegulatoryAum" => filter.SortDescending ? query.OrderByDescending(f => f.RegulatoryAum) : query.OrderBy(f => f.RegulatoryAum),
            "RegistrationDate" => filter.SortDescending ? query.OrderByDescending(f => f.RegistrationDate) : query.OrderBy(f => f.RegistrationDate),
            "UpdatedAt" => filter.SortDescending ? query.OrderByDescending(f => f.UpdatedAt) : query.OrderBy(f => f.UpdatedAt),
            _ => filter.SortDescending ? query.OrderByDescending(f => f.Name) : query.OrderBy(f => f.Name),
        };
    }

    public List<string> GetDistinctStates()
    {
        using var ctx = CreateContext();
        return ctx.Advisors.AsNoTracking()
            .Where(a => a.State != null && !a.IsExcluded)
            .Select(a => a.State!)
            .Distinct()
            .OrderBy(s => s)
            .ToList();
    }

    public List<string> GetDistinctFirmStates()
    {
        using var ctx = CreateContext();
        return ctx.Firms.AsNoTracking()
            .Where(f => f.State != null && !f.IsExcluded)
            .Select(f => f.State!)
            .Distinct()
            .OrderBy(s => s)
            .ToList();
    }

    public List<string> GetDistinctFirmNames()
    {
        using var ctx = CreateContext();
        return ctx.Advisors.AsNoTracking()
            .Where(a => a.CurrentFirmName != null && !a.IsExcluded)
            .Select(a => a.CurrentFirmName!)
            .Distinct()
            .OrderBy(n => n)
            .Take(200)
            .ToList();
    }

    public int GetAdvisorCount(SearchFilter filter)
    {
        using var ctx = CreateContext();
        return ApplyAdvisorFilters(ctx.Advisors.AsNoTracking(), filter).Count();
    }

    // ── Dashboard & Cross-Navigation ──────────────────────────────────

    public (int Total, int WithDisclosures, double DisclosurePercent, int Finra, int Sec, int Favorites, int InCrm) GetAdvisorStats()
    {
        using var ctx = CreateContext();
        var q = ctx.Advisors.AsNoTracking().Where(a => !a.IsExcluded);
        int total = q.Count();
        int withDisclosures = q.Count(a => a.HasDisclosures);
        int finra = q.Count(a => a.Source != null && a.Source.Contains("FINRA"));
        // Count advisors who are Investment Advisor Representatives (SEC-registered IARs)
        // regardless of which data source populated the record
        int sec = q.Count(a =>
            a.RecordType == "Investment Advisor Representative" ||
            (a.Source != null && a.Source.Contains("SEC")));
        int favorites = q.Count(a => a.IsFavorited);
        int inCrm = q.Count(a => a.IsImportedToCrm);
        double pct = total > 0 ? Math.Round(withDisclosures * 100.0 / total, 1) : 0;
        return (total, withDisclosures, pct, finra, sec, favorites, inCrm);
    }

    public (int Total, int InvestmentAdvisor, int BrokerDealer) GetFirmStats()
    {
        using var ctx = CreateContext();
        var q = ctx.Firms.AsNoTracking().Where(f => !f.IsExcluded);
        int total = q.Count();
        int ia = q.Count(f => f.RecordType == "Investment Adviser");
        int bd = q.Count(f => f.RecordType == "Broker-Dealer");
        return (total, ia, bd);
    }

    public (int TotalAdvisors, int TotalFirms, int Favorites, int InCrm,
            int WithDisclosures, int FavsNotInCrm, int UpdatedToday) GetDashboardStats()
    {
        using var ctx = CreateContext();
        var advisors = ctx.Advisors.AsNoTracking().Where(a => !a.IsExcluded);
        var firms = ctx.Firms.AsNoTracking().Where(f => !f.IsExcluded);

        int totalAdvisors = advisors.Count();
        int totalFirms = firms.Count();
        int favorites = advisors.Count(a => a.IsFavorited);
        int inCrm = advisors.Count(a => a.IsImportedToCrm);
        int withDisclosures = advisors.Count(a => a.HasDisclosures);
        int favsNotInCrm = advisors.Count(a => a.IsFavorited && !a.IsImportedToCrm);

        var today = DateTime.UtcNow.Date;
        int updatedToday = advisors.Count(a => a.UpdatedAt >= today);

        return (totalAdvisors, totalFirms, favorites, inCrm,
                withDisclosures, favsNotInCrm, updatedToday);
    }

    public List<Firm> GetTopMaFirms(int limit = 10)
    {
        using var ctx = CreateContext();
        return ctx.Firms.AsNoTracking()
            .Where(f => !f.IsExcluded && f.RegulatoryAum != null && f.RegulatoryAum > 0)
            .OrderByDescending(f => f.RegulatoryAum)
            .Take(limit)
            .ToList();
    }

    public List<Advisor> GetAdvisorsByFirmCrd(string firmCrd, int limit = 100)
    {
        using var ctx = CreateContext();
        return ctx.Advisors.AsNoTracking()
            .Where(a => !a.IsExcluded && a.CurrentFirmCrd == firmCrd)
            .OrderBy(a => a.LastName).ThenBy(a => a.FirstName)
            .Take(limit)
            .ToList();
    }

    public Firm? GetFirmByCrd(string crd)
    {
        using var ctx = CreateContext();
        return ctx.Firms.AsNoTracking()
            .FirstOrDefault(f => f.CrdNumber == crd);
    }

    // ── Employment Change Events ───────────────────────────────────────────

    public List<EmploymentChangeEvent> GetRecentEmploymentChanges(int days = 90)
    {
        using var ctx = CreateContext();
        var cutoff = DateTime.UtcNow.AddDays(-days);
        return ctx.EmploymentChangeEvents.AsNoTracking()
            .Where(e => e.DetectedAt >= cutoff)
            .OrderByDescending(e => e.DetectedAt)
            .ToList();
    }

    public List<AdvisorCourtRecord> GetCourtRecordsByCrd(string crd)
    {
        using var ctx = CreateContext();
        return ctx.AdvisorCourtRecords.AsNoTracking()
            .Where(r => r.AdvisorCrd == crd)
            .OrderByDescending(r => r.FilingDate)
            .ToList();
    }

    // ── Competitive Intelligence ───────────────────────────────────────────

    public List<Firm> GetFirmsForIntelligence(string? state = null, int limit = 200)
    {
        using var ctx = CreateContext();
        var query = ctx.Firms.AsNoTracking()
            .Where(f => !f.IsExcluded);
        if (!string.IsNullOrEmpty(state))
            query = query.Where(f => f.State == state);
        return query
            .OrderByDescending(f => f.NumberOfAdvisors)
            .Take(limit)
            .ToList();
    }

    public decimal? GetFirmAum1YearAgo(string firmCrd)
    {
        using var ctx = CreateContext();
        var targetDate = DateTime.UtcNow.AddYears(-1);
        var rangeStart = targetDate.AddDays(-180);
        var rangeEnd = targetDate.AddDays(180);
        var record = ctx.FirmAumHistory.AsNoTracking()
            .Where(h => h.FirmCrd == firmCrd
                && h.SnapshotDate >= rangeStart
                && h.SnapshotDate <= rangeEnd)
            .AsEnumerable()
            .OrderBy(h => Math.Abs((h.SnapshotDate - targetDate).TotalDays))
            .FirstOrDefault();
        return record?.TotalAum;
    }

    // ── Disclosure Scoring ────────────────────────────────────────────────

    public List<Advisor> GetAdvisorsWithDisclosures(int limit = 1000)
    {
        using var ctx = CreateContext();
        return ctx.Advisors.AsNoTracking()
            .Include(a => a.Disclosures)
            .Where(a => a.HasDisclosures)
            .OrderByDescending(a => a.DisclosureCount)
            .Take(limit)
            .ToList();
    }

    // ── Geographic Aggregation ─────────────────────────────────────────────

    public List<Advisor> GetAdvisorsForGeography(bool activeOnly = true)
    {
        using var ctx = CreateContext();
        var query = ctx.Advisors.AsNoTracking()
            .Where(a => !a.IsExcluded && a.State != null);
        if (activeOnly)
            query = query.Where(a => a.RegistrationStatus != null
                && a.RegistrationStatus.Contains("Active"));
        return query.ToList();
    }

    public List<Firm> GetFirmsForGeography(bool activeOnly = true)
    {
        using var ctx = CreateContext();
        return ctx.Firms.AsNoTracking()
            .Where(f => !f.IsExcluded && f.State != null)
            .ToList();
    }

    // ── Mobility Score ────────────────────────────────────────────────────

    public List<Advisor> GetActiveAdvisors(int limit = 5000)
    {
        using var ctx = CreateContext();
        return ctx.Advisors.AsNoTracking()
            .Where(a => !a.IsExcluded
                && (a.RegistrationStatus == "Active" || a.RegistrationStatus == null))
            .OrderByDescending(a => a.UpdatedAt)
            .Take(limit)
            .ToList();
    }
}
