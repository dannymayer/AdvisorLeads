using Microsoft.EntityFrameworkCore;
using AdvisorLeads.Models;
using System.Text;

namespace AdvisorLeads.Data;

public class AdvisorRepository
{
    private readonly string _dbPath;

    public AdvisorRepository(string databasePath)
    {
        _dbPath = databasePath;
    }

    private DatabaseContext CreateContext() => new DatabaseContext(_dbPath);

    // ── Advisors ──────────────────────────────────────────────────────────

    public List<Advisor> GetAdvisors(SearchFilter filter)
    {
        using var ctx = CreateContext();
        IQueryable<Advisor> query = ctx.Advisors.AsNoTracking();

        if (!filter.IncludeExcluded)
            query = query.Where(a => !a.IsExcluded);

        if (!string.IsNullOrWhiteSpace(filter.NameQuery))
        {
            var pattern = $"%{filter.NameQuery}%";
            query = query.Where(a =>
                EF.Functions.Like(a.FirstName, pattern) ||
                EF.Functions.Like(a.LastName, pattern));
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

        if (filter.MinYearsExperience.HasValue)
            query = query.Where(a => a.YearsOfExperience >= filter.MinYearsExperience.Value);

        if (filter.MaxYearsExperience.HasValue)
            query = query.Where(a => a.YearsOfExperience <= filter.MaxYearsExperience.Value);

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            var pattern = $"%{filter.City}%";
            query = query.Where(a => EF.Functions.Like(a.City!, pattern));
        }

        if (filter.MinDisclosureCount.HasValue && filter.MinDisclosureCount.Value > 0)
            query = query.Where(a => a.DisclosureCount >= filter.MinDisclosureCount.Value);

        if (filter.ShowFavoritesOnly)
            query = query.Where(a => a.IsFavorited);

        IOrderedQueryable<Advisor> ordered = filter.SortBy switch
        {
            "FirstName" => filter.SortDescending ? query.OrderByDescending(a => a.FirstName) : query.OrderBy(a => a.FirstName),
            "State" => filter.SortDescending ? query.OrderByDescending(a => a.State) : query.OrderBy(a => a.State),
            "CurrentFirmName" => filter.SortDescending ? query.OrderByDescending(a => a.CurrentFirmName) : query.OrderBy(a => a.CurrentFirmName),
            "RegistrationStatus" => filter.SortDescending ? query.OrderByDescending(a => a.RegistrationStatus) : query.OrderBy(a => a.RegistrationStatus),
            "RecordType" => filter.SortDescending ? query.OrderByDescending(a => a.RecordType) : query.OrderBy(a => a.RecordType),
            "YearsOfExperience" => filter.SortDescending ? query.OrderByDescending(a => a.YearsOfExperience) : query.OrderBy(a => a.YearsOfExperience),
            "UpdatedAt" => filter.SortDescending ? query.OrderByDescending(a => a.UpdatedAt) : query.OrderBy(a => a.UpdatedAt),
            _ => filter.SortDescending ? query.OrderByDescending(a => a.LastName) : query.OrderBy(a => a.LastName),
        };

        return ordered
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();
    }

    public Advisor? GetAdvisorById(int id)
    {
        using var ctx = CreateContext();
        return ctx.Advisors
            .Include(a => a.EmploymentHistory)
            .Include(a => a.Disclosures)
            .Include(a => a.QualificationList)
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

    public int UpsertAdvisor(Advisor advisor)
    {
        using var ctx = CreateContext();

        Advisor? existing = null;
        if (!string.IsNullOrEmpty(advisor.CrdNumber))
            existing = ctx.Advisors.FirstOrDefault(a => a.CrdNumber == advisor.CrdNumber);

        if (existing != null)
        {
            advisor.Id = existing.Id;
            UpdateAdvisor(ctx, existing, advisor);
        }
        else
        {
            InsertAdvisor(ctx, advisor);
        }

        // Upsert related data
        if (advisor.EmploymentHistory.Count > 0)
            UpsertEmploymentHistory(ctx, advisor.Id, advisor.EmploymentHistory);
        if (advisor.Disclosures.Count > 0)
            UpsertDisclosures(ctx, advisor.Id, advisor.Disclosures);
        if (advisor.QualificationList.Count > 0)
            UpsertQualifications(ctx, advisor.Id, advisor.QualificationList);

        return advisor.Id;
    }

    private void InsertAdvisor(DatabaseContext ctx, Advisor a)
    {
        a.IsExcluded = false;
        a.IsImportedToCrm = false;
        a.UpdatedAt = DateTime.UtcNow;
        ctx.Advisors.Add(a);
        ctx.SaveChanges();
    }

    private void UpdateAdvisor(DatabaseContext ctx, Advisor existing, Advisor incoming)
    {
        // COALESCE logic: only overwrite if incoming value is non-null/non-empty
        if (!string.IsNullOrEmpty(incoming.IapdNumber))
            existing.IapdNumber = incoming.IapdNumber;

        existing.FirstName = incoming.FirstName;
        existing.LastName = incoming.LastName;

        if (incoming.MiddleName != null)
            existing.MiddleName = incoming.MiddleName;
        if (incoming.Title != null)
            existing.Title = incoming.Title;
        if (incoming.Email != null)
            existing.Email = incoming.Email;
        if (incoming.Phone != null)
            existing.Phone = incoming.Phone;
        if (!string.IsNullOrEmpty(incoming.City))
            existing.City = incoming.City;
        if (!string.IsNullOrEmpty(incoming.State))
            existing.State = incoming.State;
        if (!string.IsNullOrEmpty(incoming.ZipCode))
            existing.ZipCode = incoming.ZipCode;
        if (!string.IsNullOrEmpty(incoming.Licenses))
            existing.Licenses = incoming.Licenses;
        if (!string.IsNullOrEmpty(incoming.Qualifications))
            existing.Qualifications = incoming.Qualifications;
        if (!string.IsNullOrEmpty(incoming.CurrentFirmName))
            existing.CurrentFirmName = incoming.CurrentFirmName;
        if (!string.IsNullOrEmpty(incoming.CurrentFirmCrd))
            existing.CurrentFirmCrd = incoming.CurrentFirmCrd;
        if (incoming.CurrentFirmId != null)
            existing.CurrentFirmId = incoming.CurrentFirmId;
        if (!string.IsNullOrEmpty(incoming.RegistrationStatus))
            existing.RegistrationStatus = incoming.RegistrationStatus;
        if (incoming.RegistrationDate.HasValue)
            existing.RegistrationDate = incoming.RegistrationDate;
        if (incoming.YearsOfExperience != null)
            existing.YearsOfExperience = incoming.YearsOfExperience;

        // HasDisclosures / DisclosureCount: use Math.Max
        existing.HasDisclosures = existing.HasDisclosures || incoming.HasDisclosures;
        existing.DisclosureCount = Math.Max(existing.DisclosureCount, incoming.DisclosureCount);

        // Source: append if not already present
        if (!string.IsNullOrEmpty(incoming.Source))
        {
            if (string.IsNullOrEmpty(existing.Source))
            {
                existing.Source = incoming.Source;
            }
            else if (!("," + existing.Source + ",").Contains("," + incoming.Source + ","))
            {
                existing.Source = existing.Source + "," + incoming.Source;
            }
        }

        if (!string.IsNullOrEmpty(incoming.RecordType))
            existing.RecordType = incoming.RecordType;
        if (incoming.Suffix != null)
            existing.Suffix = incoming.Suffix;
        if (incoming.IapdLink != null)
            existing.IapdLink = incoming.IapdLink;
        if (!string.IsNullOrEmpty(incoming.RegAuthorities))
            existing.RegAuthorities = incoming.RegAuthorities;
        if (!string.IsNullOrEmpty(incoming.DisclosureFlags))
            existing.DisclosureFlags = incoming.DisclosureFlags;
        if (!string.IsNullOrEmpty(incoming.OtherNames))
            existing.OtherNames = incoming.OtherNames;

        existing.UpdatedAt = DateTime.UtcNow;
        ctx.SaveChanges();
    }

    public void SetAdvisorExcluded(int id, bool excluded, string? reason = null)
    {
        using var ctx = CreateContext();
        ctx.Advisors
            .Where(a => a.Id == id)
            .ExecuteUpdate(s => s
                .SetProperty(a => a.IsExcluded, excluded)
                .SetProperty(a => a.ExclusionReason, reason)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow));
    }

    public void SetAdvisorImported(int id, string? crmId)
    {
        using var ctx = CreateContext();
        ctx.Advisors
            .Where(a => a.Id == id)
            .ExecuteUpdate(s => s
                .SetProperty(a => a.IsImportedToCrm, true)
                .SetProperty(a => a.CrmId, crmId)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow));
    }

    public void SetAdvisorFavorited(int id, bool favorited)
    {
        using var ctx = CreateContext();
        ctx.Advisors
            .Where(a => a.Id == id)
            .ExecuteUpdate(s => s
                .SetProperty(a => a.IsFavorited, favorited));
    }

    private void UpsertEmploymentHistory(DatabaseContext ctx, int advisorId, List<EmploymentHistory> history)
    {
        var existing = ctx.EmploymentHistory
            .Where(e => e.AdvisorId == advisorId)
            .ToList();

        foreach (var h in history)
        {
            if (string.IsNullOrWhiteSpace(h.FirmName)) continue;

            var match = existing.FirstOrDefault(e =>
                string.Equals(e.FirmName, h.FirmName, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                bool hasNewStart = h.StartDate.HasValue;
                bool hasNewEnd = h.EndDate.HasValue && h.EndDate.Value != DateTime.MinValue;
                bool hasNewPos = !string.IsNullOrWhiteSpace(h.Position);
                bool hasNewCrd = !string.IsNullOrWhiteSpace(h.FirmCrd);

                if (hasNewStart || hasNewEnd || hasNewPos || hasNewCrd)
                {
                    if (hasNewStart) match.StartDate = h.StartDate;
                    if (hasNewEnd) match.EndDate = h.EndDate;
                    if (hasNewPos) match.Position = h.Position;
                    if (hasNewCrd) match.FirmCrd = h.FirmCrd;
                    if (!string.IsNullOrWhiteSpace(h.Street)) match.Street = h.Street;
                }
            }
            else
            {
                var entry = new EmploymentHistory
                {
                    AdvisorId = advisorId,
                    FirmName = h.FirmName,
                    FirmCrd = h.FirmCrd,
                    StartDate = h.StartDate,
                    EndDate = (h.EndDate.HasValue && h.EndDate.Value != DateTime.MinValue) ? h.EndDate : null,
                    Position = h.Position,
                    Street = h.Street
                };
                ctx.EmploymentHistory.Add(entry);
                existing.Add(entry); // prevent double-insert within same batch
            }
        }

        ctx.SaveChanges();
    }

    private void UpsertDisclosures(DatabaseContext ctx, int advisorId, List<Disclosure> disclosures)
    {
        ctx.Disclosures.Where(d => d.AdvisorId == advisorId).ExecuteDelete();

        foreach (var d in disclosures)
            d.AdvisorId = advisorId;

        ctx.Disclosures.AddRange(disclosures);
        ctx.SaveChanges();
    }

    private void UpsertQualifications(DatabaseContext ctx, int advisorId, List<Qualification> qualifications)
    {
        ctx.Qualifications.Where(q => q.AdvisorId == advisorId).ExecuteDelete();

        foreach (var q in qualifications)
            q.AdvisorId = advisorId;

        ctx.Qualifications.AddRange(qualifications);
        ctx.SaveChanges();
    }

    public List<string> GetCrdsNeedingEnrichment(int limit)
    {
        using var ctx = CreateContext();
        return ctx.Advisors.AsNoTracking()
            .Where(a => a.CrdNumber != null
                && a.RegistrationStatus == "Active"
                && !a.QualificationList.Any())
            .OrderBy(a => a.UpdatedAt)
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

    // ── Firms ─────────────────────────────────────────────────────────────

    public List<Firm> GetFirms(FirmSearchFilter? filter = null)
    {
        using var ctx = CreateContext();
        filter ??= new FirmSearchFilter();

        IQueryable<Firm> query = ctx.Firms.AsNoTracking()
            .Where(f => !f.IsExcluded);

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

        IOrderedQueryable<Firm> ordered = filter.SortBy switch
        {
            "State" => filter.SortDescending ? query.OrderByDescending(f => f.State) : query.OrderBy(f => f.State),
            "NumberOfAdvisors" => filter.SortDescending ? query.OrderByDescending(f => f.NumberOfAdvisors) : query.OrderBy(f => f.NumberOfAdvisors),
            "RegistrationDate" => filter.SortDescending ? query.OrderByDescending(f => f.RegistrationDate) : query.OrderBy(f => f.RegistrationDate),
            "UpdatedAt" => filter.SortDescending ? query.OrderByDescending(f => f.UpdatedAt) : query.OrderBy(f => f.UpdatedAt),
            _ => filter.SortDescending ? query.OrderByDescending(f => f.Name) : query.OrderBy(f => f.Name),
        };

        return ordered.ToList();
    }

    public int UpsertFirm(Firm firm)
    {
        using var ctx = CreateContext();

        Firm? existing = null;
        if (!string.IsNullOrEmpty(firm.CrdNumber))
            existing = ctx.Firms.FirstOrDefault(f => f.CrdNumber == firm.CrdNumber);

        if (existing != null)
        {
            firm.Id = existing.Id;
            existing.Name = firm.Name;
            existing.Address = firm.Address;
            existing.City = firm.City;
            existing.State = firm.State;
            existing.ZipCode = firm.ZipCode;
            existing.Phone = firm.Phone;
            existing.Website = firm.Website;
            existing.BusinessType = firm.BusinessType;
            existing.IsRegisteredWithSec = firm.IsRegisteredWithSec;
            existing.IsRegisteredWithFinra = firm.IsRegisteredWithFinra;
            existing.NumberOfAdvisors = firm.NumberOfAdvisors;
            existing.RegistrationDate = firm.RegistrationDate;
            existing.Source = firm.Source;
            existing.RecordType = firm.RecordType;
            existing.SECNumber = firm.SECNumber;
            existing.SECRegion = firm.SECRegion;
            existing.LegalName = firm.LegalName;
            existing.FaxPhone = firm.FaxPhone;
            existing.MailingAddress = firm.MailingAddress;
            existing.RegistrationStatus = firm.RegistrationStatus;
            existing.AumDescription = firm.AumDescription;
            existing.StateOfOrganization = firm.StateOfOrganization;
            existing.RegulatoryAum = firm.RegulatoryAum;
            existing.RegulatoryAumNonDiscretionary = firm.RegulatoryAumNonDiscretionary;
            existing.NumClients = firm.NumClients;
            existing.BrokerProtocolMember = firm.BrokerProtocolMember;
            existing.BrokerProtocolUpdatedAt = firm.BrokerProtocolUpdatedAt;
            existing.UpdatedAt = DateTime.UtcNow;
            ctx.SaveChanges();
        }
        else
        {
            firm.UpdatedAt = DateTime.UtcNow;
            ctx.Firms.Add(firm);
            ctx.SaveChanges();
        }

        return firm.Id;
    }

    /// <summary>
    /// Efficiently upserts a large batch of SEC firm records using a single transaction.
    /// Uses SQLite ON CONFLICT to update existing rows without changing their Id.
    /// </summary>
    public void UpsertFirmBatch(IEnumerable<Firm> firms, IProgress<string>? progress = null)
    {
        using var ctx = CreateContext();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();

        using var txn = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = txn;
        cmd.CommandText = @"
            INSERT INTO Firms (CrdNumber, Name, LegalName, SECNumber, SECRegion,
                Address, City, State, Country, ZipCode, Phone, FaxPhone, Website,
                MailingAddress, BusinessType, StateOfOrganization,
                RecordType, RegistrationStatus, RegistrationDate, LatestFilingDate,
                NumberOfAdvisors, NumberOfEmployees, AumDescription,
                RegulatoryAum, RegulatoryAumNonDiscretionary, NumClients,
                IsRegisteredWithSec, Source, CreatedAt, UpdatedAt)
            VALUES (@crd, @name, @legal, @sec, @region,
                @addr, @city, @state, @country, @zip, @phone, @fax, @web,
                @mail, @btype, @stateOrg,
                @rectype, @regstatus, @regdate, @filingdate,
                @numadv, @numemp, @aum,
                @regaum, @regaumnd, @numclients,
                1, 'SEC', datetime('now'), datetime('now'))
            ON CONFLICT(CrdNumber) DO UPDATE SET
                Name               = excluded.Name,
                LegalName          = coalesce(excluded.LegalName, LegalName),
                SECNumber          = coalesce(excluded.SECNumber, SECNumber),
                SECRegion          = coalesce(excluded.SECRegion, SECRegion),
                Address            = coalesce(excluded.Address, Address),
                City               = coalesce(excluded.City, City),
                State              = coalesce(excluded.State, State),
                Country            = coalesce(excluded.Country, Country),
                ZipCode            = coalesce(excluded.ZipCode, ZipCode),
                Phone              = coalesce(excluded.Phone, Phone),
                FaxPhone           = coalesce(excluded.FaxPhone, FaxPhone),
                Website            = coalesce(excluded.Website, Website),
                MailingAddress     = coalesce(excluded.MailingAddress, MailingAddress),
                BusinessType       = coalesce(excluded.BusinessType, BusinessType),
                StateOfOrganization = coalesce(excluded.StateOfOrganization, StateOfOrganization),
                RecordType         = excluded.RecordType,
                RegistrationStatus = coalesce(excluded.RegistrationStatus, RegistrationStatus),
                RegistrationDate   = coalesce(excluded.RegistrationDate, RegistrationDate),
                LatestFilingDate   = coalesce(excluded.LatestFilingDate, LatestFilingDate),
                NumberOfAdvisors   = coalesce(excluded.NumberOfAdvisors, NumberOfAdvisors),
                NumberOfEmployees  = coalesce(excluded.NumberOfEmployees, NumberOfEmployees),
                AumDescription     = coalesce(excluded.AumDescription, AumDescription),
                RegulatoryAum      = coalesce(excluded.RegulatoryAum, RegulatoryAum),
                RegulatoryAumNonDiscretionary = coalesce(excluded.RegulatoryAumNonDiscretionary, RegulatoryAumNonDiscretionary),
                NumClients         = coalesce(excluded.NumClients, NumClients),
                IsRegisteredWithSec = 1,
                Source             = 'SEC',
                UpdatedAt          = datetime('now')
        ";

        int count = 0;
        foreach (var f in firms)
        {
            cmd.Parameters.Clear();

            void AddParam(string name, object? value) {
                var p = cmd.CreateParameter();
                p.ParameterName = name;
                p.Value = value ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }

            AddParam("@crd", f.CrdNumber);
            AddParam("@name", f.Name);
            AddParam("@legal", f.LegalName);
            AddParam("@sec", f.SECNumber);
            AddParam("@region", f.SECRegion);
            AddParam("@addr", f.Address);
            AddParam("@city", f.City);
            AddParam("@state", f.State);
            AddParam("@country", f.Country);
            AddParam("@zip", f.ZipCode);
            AddParam("@phone", f.Phone);
            AddParam("@fax", f.FaxPhone);
            AddParam("@web", f.Website);
            AddParam("@mail", f.MailingAddress);
            AddParam("@btype", f.BusinessType);
            AddParam("@stateOrg", f.StateOfOrganization);
            AddParam("@rectype", f.RecordType);
            AddParam("@regstatus", f.RegistrationStatus);
            AddParam("@regdate", f.RegistrationDate.HasValue
                ? (object)f.RegistrationDate.Value.ToString("yyyy-MM-dd") : null);
            AddParam("@filingdate", f.LatestFilingDate);
            AddParam("@numadv", f.NumberOfAdvisors);
            AddParam("@numemp", f.NumberOfEmployees);
            AddParam("@aum", f.AumDescription);
            AddParam("@regaum", f.RegulatoryAum);
            AddParam("@regaumnd", f.RegulatoryAumNonDiscretionary);
            AddParam("@numclients", f.NumClients);
            cmd.ExecuteNonQuery();
            count++;
            if (count % 1000 == 0)
                progress?.Report($"SEC Monthly: Saved {count:N0} firms...");
        }

        txn.Commit();
        progress?.Report($"SEC Monthly: Saved {count:N0} total firms to database.");
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
        IQueryable<Advisor> query = ctx.Advisors.AsNoTracking();

        if (!filter.IncludeExcluded)
            query = query.Where(a => !a.IsExcluded);

        if (!string.IsNullOrWhiteSpace(filter.NameQuery))
        {
            var pattern = $"%{filter.NameQuery}%";
            query = query.Where(a =>
                EF.Functions.Like(a.FirstName, pattern) ||
                EF.Functions.Like(a.LastName, pattern));
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

        if (filter.MinYearsExperience.HasValue)
            query = query.Where(a => a.YearsOfExperience >= filter.MinYearsExperience.Value);

        if (filter.MaxYearsExperience.HasValue)
            query = query.Where(a => a.YearsOfExperience <= filter.MaxYearsExperience.Value);

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            var pattern = $"%{filter.City}%";
            query = query.Where(a => EF.Functions.Like(a.City!, pattern));
        }

        if (filter.MinDisclosureCount.HasValue && filter.MinDisclosureCount.Value > 0)
            query = query.Where(a => a.DisclosureCount >= filter.MinDisclosureCount.Value);

        if (filter.ShowFavoritesOnly)
            query = query.Where(a => a.IsFavorited);

        return query.Count();
    }

    /// <summary>
    /// Marks firms as Broker Protocol members by matching against a list of member names.
    /// Clears old memberships first, then sets new ones using fuzzy name matching.
    /// </summary>
    public int UpdateBrokerProtocolStatus(List<string> memberNames, DateTime fetchedAt)
    {
        using var ctx = CreateContext();

        // Clear all existing memberships
        ctx.Firms.ExecuteUpdate(s => s
            .SetProperty(f => f.BrokerProtocolMember, false));

        if (memberNames.Count == 0) return 0;

        // Load all firm names for fuzzy matching
        var firms = ctx.Firms.AsNoTracking()
            .Where(f => f.Name != null)
            .Select(f => new { f.Id, f.Name })
            .ToList();

        // Normalize a name for matching: lowercase, strip legal suffixes, remove punctuation
        static string Normalize(string s) => System.Text.RegularExpressions.Regex.Replace(
            s.ToLowerInvariant()
             .Replace(" llc", "").Replace(" inc", "").Replace(" corp", "")
             .Replace(" lp", "").Replace(" ltd", "").Replace(" co.", "")
             .Replace(",", "").Replace(".", "").Replace("&", "and"),
            @"\s+", " ").Trim();

        var memberNormalized = memberNames.Select(Normalize).ToHashSet();

        var toUpdate = new List<int>();
        foreach (var f in firms)
        {
            var norm = Normalize(f.Name);
            if (memberNormalized.Contains(norm) ||
                memberNormalized.Any(m => norm.Contains(m) || m.Contains(norm)))
            {
                toUpdate.Add(f.Id);
            }
        }

        int updated = 0;
        if (toUpdate.Count > 0)
        {
            updated = ctx.Firms
                .Where(f => toUpdate.Contains(f.Id))
                .ExecuteUpdate(s => s
                    .SetProperty(f => f.BrokerProtocolMember, true)
                    .SetProperty(f => f.BrokerProtocolUpdatedAt, fetchedAt));
        }

        return updated;
    }
}
