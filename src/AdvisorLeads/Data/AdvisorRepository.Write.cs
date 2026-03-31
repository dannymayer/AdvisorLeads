using Microsoft.EntityFrameworkCore;
using AdvisorLeads.Abstractions;
using AdvisorLeads.Models;
using System.Text;

namespace AdvisorLeads.Data;

public partial class AdvisorRepository
{
    /// <summary>Returns the advisor's database Id. Sets <paramref name="wasNew"/> to true when a new record was inserted.</summary>
    public int UpsertAdvisor(Advisor advisor, out bool wasNew, Services.WatchListMonitorService? watchMonitor = null)
    {
        using var ctx = CreateContext();

        Advisor? existing = null;
        if (!string.IsNullOrEmpty(advisor.CrdNumber))
            existing = ctx.Advisors.FirstOrDefault(a => a.CrdNumber == advisor.CrdNumber);

        wasNew = existing == null;

        if (existing != null)
        {
            advisor.Id = existing.Id;
            if (existing.IsWatched && watchMonitor != null)
                watchMonitor.RecordNewAdvisorAlerts(existing, advisor);
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

        if (advisor.Registrations.Count > 0)
            UpsertRegistrations(ctx, advisor.Id, advisor.Registrations);

        return advisor.Id;
    }

    /// <summary>Convenience overload for callers that don't need the new/updated distinction.</summary>
    public int UpsertAdvisor(Advisor advisor) => UpsertAdvisor(advisor, out _);

    private void InsertAdvisor(DatabaseContext ctx, Advisor a)
    {
        a.IsExcluded = false;
        a.IsImportedToCrm = false;
        a.UpdatedAt = DateTime.UtcNow;
        if (!a.FirstSeenAt.HasValue)
            a.FirstSeenAt = DateTime.UtcNow;
        ctx.Advisors.Add(a);
        ctx.SaveChanges();
    }

    private void UpdateAdvisor(DatabaseContext ctx, Advisor existing, Advisor incoming)
    {
        var fromFirmCrd = existing.CurrentFirmCrd;
        var fromFirmName = existing.CurrentFirmName;

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
        {
            bool firmExists = ctx.Firms.Any(f => f.Id == incoming.CurrentFirmId.Value);
            existing.CurrentFirmId = firmExists ? incoming.CurrentFirmId : null;
        }
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

        if (!string.IsNullOrEmpty(incoming.BcScope))
            existing.BcScope = incoming.BcScope;
        if (!string.IsNullOrEmpty(incoming.IaScope))
            existing.IaScope = incoming.IaScope;
        if (!string.IsNullOrEmpty(incoming.RegistrationLevel))
            existing.RegistrationLevel = incoming.RegistrationLevel;

        // Accumulate disclosure type flags across sources
        existing.HasCriminalDisclosure = existing.HasCriminalDisclosure || incoming.HasCriminalDisclosure;
        existing.HasRegulatoryDisclosure = existing.HasRegulatoryDisclosure || incoming.HasRegulatoryDisclosure;
        existing.HasCivilDisclosure = existing.HasCivilDisclosure || incoming.HasCivilDisclosure;
        existing.HasCustomerComplaintDisclosure = existing.HasCustomerComplaintDisclosure || incoming.HasCustomerComplaintDisclosure;
        existing.HasFinancialDisclosure = existing.HasFinancialDisclosure || incoming.HasFinancialDisclosure;
        existing.HasTerminationDisclosure = existing.HasTerminationDisclosure || incoming.HasTerminationDisclosure;

        existing.BcDisclosureCount = Math.Max(existing.BcDisclosureCount, incoming.BcDisclosureCount);
        existing.IaDisclosureCount = Math.Max(existing.IaDisclosureCount, incoming.IaDisclosureCount);

        if (incoming.CareerStartDate.HasValue &&
            (!existing.CareerStartDate.HasValue || incoming.CareerStartDate.Value < existing.CareerStartDate.Value))
            existing.CareerStartDate = incoming.CareerStartDate;

        if (incoming.TotalFirmCount.HasValue)
            existing.TotalFirmCount = Math.Max(existing.TotalFirmCount ?? 0, incoming.TotalFirmCount.Value);

        if (!string.IsNullOrEmpty(incoming.BrokerCheckUrl))
            existing.BrokerCheckUrl = incoming.BrokerCheckUrl;

        // Detect firm change and record employment change event
        if (!string.IsNullOrEmpty(incoming.CurrentFirmCrd)
            && !string.IsNullOrEmpty(existing.CurrentFirmCrd)
            && !string.Equals(existing.CurrentFirmCrd, incoming.CurrentFirmCrd, StringComparison.OrdinalIgnoreCase))
        {
            var changeEvent = new EmploymentChangeEvent
            {
                AdvisorId = existing.Id,
                AdvisorCrd = existing.CrdNumber ?? string.Empty,
                FromFirmCrd = existing.CurrentFirmCrd,
                FromFirmName = existing.CurrentFirmName,
                ToFirmCrd = incoming.CurrentFirmCrd,
                ToFirmName = incoming.CurrentFirmName,
                DetectedAt = DateTime.UtcNow
            };
            ctx.EmploymentChangeEvents.Add(changeEvent);
            existing.HasRecentFirmChange = true;
            existing.FirmChangeDetectedAt = DateTime.UtcNow;
        }
        existing.LastEmploymentCheckDate = DateTime.UtcNow;

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

    public void SetAdvisorWatched(int id, bool watched)
    {
        using var ctx = CreateContext();
        ctx.Advisors
            .Where(a => a.Id == id)
            .ExecuteUpdate(s => s
                .SetProperty(a => a.IsWatched, watched));
    }

    public void SetFirmWatched(int id, bool watched)
    {
        using var ctx = CreateContext();
        ctx.Firms
            .Where(f => f.Id == id)
            .ExecuteUpdate(s => s
                .SetProperty(f => f.IsWatched, watched));
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

                if (!string.IsNullOrWhiteSpace(h.FirmCity)) match.FirmCity = h.FirmCity;
                if (!string.IsNullOrWhiteSpace(h.FirmState)) match.FirmState = h.FirmState;
                if (!string.IsNullOrWhiteSpace(h.RegistrationCategories)) match.RegistrationCategories = h.RegistrationCategories;
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
                    Street = h.Street,
                    FirmCity = string.IsNullOrWhiteSpace(h.FirmCity) ? null : h.FirmCity,
                    FirmState = string.IsNullOrWhiteSpace(h.FirmState) ? null : h.FirmState,
                    RegistrationCategories = string.IsNullOrWhiteSpace(h.RegistrationCategories) ? null : h.RegistrationCategories
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

    private void UpsertRegistrations(DatabaseContext ctx, int advisorId, List<AdvisorRegistration> registrations)
    {
        ctx.AdvisorRegistrations.Where(r => r.AdvisorId == advisorId).ExecuteDelete();

        foreach (var r in registrations)
            r.AdvisorId = advisorId;

        ctx.AdvisorRegistrations.AddRange(registrations);
        ctx.SaveChanges();
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
            existing.NumberOfEmployees = firm.NumberOfEmployees ?? existing.NumberOfEmployees;
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
            existing.Country = firm.Country ?? existing.Country;
            existing.LatestFilingDate = firm.LatestFilingDate ?? existing.LatestFilingDate;
            existing.RegulatoryAum = firm.RegulatoryAum;
            existing.RegulatoryAumNonDiscretionary = firm.RegulatoryAumNonDiscretionary;
            existing.TotalAumRelatedPersons = firm.TotalAumRelatedPersons ?? existing.TotalAumRelatedPersons;
            existing.NumClients = firm.NumClients;
            existing.ClientsIndividuals = firm.ClientsIndividuals ?? existing.ClientsIndividuals;
            existing.ClientsHighNetWorth = firm.ClientsHighNetWorth ?? existing.ClientsHighNetWorth;
            existing.ClientsBankingInstitutions = firm.ClientsBankingInstitutions ?? existing.ClientsBankingInstitutions;
            existing.ClientsInvestmentCompanies = firm.ClientsInvestmentCompanies ?? existing.ClientsInvestmentCompanies;
            existing.ClientsPensionPlans = firm.ClientsPensionPlans ?? existing.ClientsPensionPlans;
            existing.ClientsCharitable = firm.ClientsCharitable ?? existing.ClientsCharitable;
            existing.ClientsGovernment = firm.ClientsGovernment ?? existing.ClientsGovernment;
            existing.ClientsOther = firm.ClientsOther ?? existing.ClientsOther;
            existing.NumberOfOffices = firm.NumberOfOffices ?? existing.NumberOfOffices;
            existing.PrivateFundCount = firm.PrivateFundCount ?? existing.PrivateFundCount;
            existing.PrivateFundGrossAssets = firm.PrivateFundGrossAssets ?? existing.PrivateFundGrossAssets;
            existing.AdvisoryActivities = firm.AdvisoryActivities ?? existing.AdvisoryActivities;
            existing.CompensationFeeOnly = firm.CompensationFeeOnly ?? existing.CompensationFeeOnly;
            existing.CompensationCommission = firm.CompensationCommission ?? existing.CompensationCommission;
            existing.CompensationHourly = firm.CompensationHourly ?? existing.CompensationHourly;
            existing.CompensationPerformanceBased = firm.CompensationPerformanceBased ?? existing.CompensationPerformanceBased;
            existing.HasCustody = firm.HasCustody ?? existing.HasCustody;
            existing.HasDiscretionaryAuthority = firm.HasDiscretionaryAuthority ?? existing.HasDiscretionaryAuthority;
            existing.IsBrokerDealer = firm.IsBrokerDealer ?? existing.IsBrokerDealer;
            existing.IsInsuranceCompany = firm.IsInsuranceCompany ?? existing.IsInsuranceCompany;
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
                TotalAumRelatedPersons, NumberOfOffices, PrivateFundCount, PrivateFundGrossAssets,
                AdvisoryActivities, CompensationFeeOnly, CompensationCommission,
                CompensationHourly, CompensationPerformanceBased,
                HasCustody, HasDiscretionaryAuthority,
                IsBrokerDealer, IsInsuranceCompany,
                ClientsIndividuals, ClientsHighNetWorth, ClientsBankingInstitutions,
                ClientsInvestmentCompanies, ClientsPensionPlans, ClientsCharitable,
                ClientsGovernment, ClientsOther,
                IsRegisteredWithSec, Source, CreatedAt, UpdatedAt)
            VALUES (@crd, @name, @legal, @sec, @region,
                @addr, @city, @state, @country, @zip, @phone, @fax, @web,
                @mail, @btype, @stateOrg,
                @rectype, @regstatus, @regdate, @filingdate,
                @numadv, @numemp, @aum,
                @regaum, @regaumnd, @numclients,
                @totalaumrel, @numoffices, @privfundcount, @privfundassets,
                @advactivities, @compfee, @compcomm,
                @comphr, @compperf,
                @custody, @discretion,
                @isbd, @isins,
                @clindiv, @clhnw, @clbank,
                @clinvest, @clpension, @clcharitable,
                @clgov, @clother,
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
                TotalAumRelatedPersons = coalesce(excluded.TotalAumRelatedPersons, TotalAumRelatedPersons),
                NumberOfOffices    = coalesce(excluded.NumberOfOffices, NumberOfOffices),
                PrivateFundCount   = coalesce(excluded.PrivateFundCount, PrivateFundCount),
                PrivateFundGrossAssets = coalesce(excluded.PrivateFundGrossAssets, PrivateFundGrossAssets),
                AdvisoryActivities = coalesce(excluded.AdvisoryActivities, AdvisoryActivities),
                CompensationFeeOnly = coalesce(excluded.CompensationFeeOnly, CompensationFeeOnly),
                CompensationCommission = coalesce(excluded.CompensationCommission, CompensationCommission),
                CompensationHourly = coalesce(excluded.CompensationHourly, CompensationHourly),
                CompensationPerformanceBased = coalesce(excluded.CompensationPerformanceBased, CompensationPerformanceBased),
                HasCustody         = coalesce(excluded.HasCustody, HasCustody),
                HasDiscretionaryAuthority = coalesce(excluded.HasDiscretionaryAuthority, HasDiscretionaryAuthority),
                IsBrokerDealer     = coalesce(excluded.IsBrokerDealer, IsBrokerDealer),
                IsInsuranceCompany = coalesce(excluded.IsInsuranceCompany, IsInsuranceCompany),
                ClientsIndividuals = coalesce(excluded.ClientsIndividuals, ClientsIndividuals),
                ClientsHighNetWorth = coalesce(excluded.ClientsHighNetWorth, ClientsHighNetWorth),
                ClientsBankingInstitutions = coalesce(excluded.ClientsBankingInstitutions, ClientsBankingInstitutions),
                ClientsInvestmentCompanies = coalesce(excluded.ClientsInvestmentCompanies, ClientsInvestmentCompanies),
                ClientsPensionPlans = coalesce(excluded.ClientsPensionPlans, ClientsPensionPlans),
                ClientsCharitable  = coalesce(excluded.ClientsCharitable, ClientsCharitable),
                ClientsGovernment  = coalesce(excluded.ClientsGovernment, ClientsGovernment),
                ClientsOther       = coalesce(excluded.ClientsOther, ClientsOther),
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
            AddParam("@totalaumrel", f.TotalAumRelatedPersons);
            AddParam("@numoffices", f.NumberOfOffices);
            AddParam("@privfundcount", f.PrivateFundCount);
            AddParam("@privfundassets", f.PrivateFundGrossAssets);
            AddParam("@advactivities", f.AdvisoryActivities);
            AddParam("@compfee", f.CompensationFeeOnly);
            AddParam("@compcomm", f.CompensationCommission);
            AddParam("@comphr", f.CompensationHourly);
            AddParam("@compperf", f.CompensationPerformanceBased);
            AddParam("@custody", f.HasCustody);
            AddParam("@discretion", f.HasDiscretionaryAuthority);
            AddParam("@isbd", f.IsBrokerDealer);
            AddParam("@isins", f.IsInsuranceCompany);
            AddParam("@clindiv", f.ClientsIndividuals);
            AddParam("@clhnw", f.ClientsHighNetWorth);
            AddParam("@clbank", f.ClientsBankingInstitutions);
            AddParam("@clinvest", f.ClientsInvestmentCompanies);
            AddParam("@clpension", f.ClientsPensionPlans);
            AddParam("@clcharitable", f.ClientsCharitable);
            AddParam("@clgov", f.ClientsGovernment);
            AddParam("@clother", f.ClientsOther);
            cmd.ExecuteNonQuery();
            count++;
            if (count % 1000 == 0)
                progress?.Report($"SEC Monthly: Saved {count:N0} firms...");
        }

        txn.Commit();
        progress?.Report($"SEC Monthly: Saved {count:N0} total firms to database.");
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
            .Select(f => new { f.Id, f.Name, f.LegalName })
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
            var norm = Normalize(f.Name!);
            var legalNorm = f.LegalName != null ? Normalize(f.LegalName) : null;
            if (memberNormalized.Contains(norm) ||
                (legalNorm != null && memberNormalized.Contains(legalNorm)) ||
                memberNormalized.Any(m =>
                    norm.Contains(m) || m.Contains(norm) ||
                    (legalNorm != null && (legalNorm.Contains(m) || m.Contains(legalNorm)))))
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

    /// <summary>
    /// Updates the Email field for the given (AdvisorId, Email) pairs.
    /// Returns the number of advisors updated.
    /// </summary>
    public int UpdateAdvisorEmails(IEnumerable<(int AdvisorId, string Email)> updates)
    {
        using var ctx = CreateContext();
        int count = 0;
        foreach (var (advisorId, email) in updates)
        {
            count += ctx.Advisors
                .Where(a => a.Id == advisorId)
                .ExecuteUpdate(s => s
                    .SetProperty(a => a.Email, email)
                    .SetProperty(a => a.UpdatedAt, DateTime.UtcNow));
        }
        return count;
    }

    public void UpsertAdvisorSanctions(string crd, List<FinraSanction> sanctions)
    {
        using var ctx = CreateContext();
        ctx.FinraSanctions
            .Where(s => s.AdvisorCrd == crd)
            .ExecuteDelete();

        foreach (var s in sanctions)
        {
            s.AdvisorCrd = crd;
            s.CreatedAt = DateTime.UtcNow;
        }
        ctx.FinraSanctions.AddRange(sanctions);
        ctx.SaveChanges();
    }

    public void UpdateAdvisorSanctionFlags(string crd, bool hasActive, decimal? maxFine,
        string? sanctionType, DateTime enrichedAt)
    {
        using var ctx = CreateContext();
        ctx.Advisors.Where(a => a.CrdNumber == crd)
            .ExecuteUpdate(s => s
                .SetProperty(a => a.HasActiveSanction, hasActive)
                .SetProperty(a => a.MaxFineAmount, maxFine)
                .SetProperty(a => a.SanctionType, sanctionType)
                .SetProperty(a => a.SanctionEnrichedAt, enrichedAt)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow));
    }

    public void UpsertSecEnforcementActions(string crd, List<SecEnforcementAction> actions)
    {
        using var ctx = CreateContext();
        ctx.SecEnforcementActions
            .Where(a => a.AdvisorCrd == crd)
            .ExecuteDelete();

        foreach (var a in actions)
        {
            a.AdvisorCrd = crd;
            a.CreatedAt = DateTime.UtcNow;
        }
        ctx.SecEnforcementActions.AddRange(actions);
        ctx.SaveChanges();
    }

    public void UpdateAdvisorSecEnforcementFlags(string crd, bool hasAction, DateTime enrichedAt)
    {
        using var ctx = CreateContext();
        ctx.Advisors.Where(a => a.CrdNumber == crd)
            .ExecuteUpdate(s => s
                .SetProperty(a => a.HasSecEnforcementAction, hasAction)
                .SetProperty(a => a.SecEnforcementEnrichedAt, enrichedAt)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow));
    }

    public void UpsertAdvisorCourtRecords(string crd, int? advisorId, List<AdvisorCourtRecord> records)
    {
        using var ctx = CreateContext();
        ctx.AdvisorCourtRecords
            .Where(r => r.AdvisorCrd == crd)
            .ExecuteDelete();

        foreach (var r in records)
        {
            r.AdvisorCrd = crd;
            r.AdvisorId = advisorId;
        }
        ctx.AdvisorCourtRecords.AddRange(records);
        ctx.SaveChanges();
    }

    public void UpdateAdvisorCourtRecordFlags(string crd, bool hasFlag, string? url,
        DateTime? date, string? summary, DateTime enrichedAt)
    {
        using var ctx = CreateContext();
        ctx.Advisors.Where(a => a.CrdNumber == crd)
            .ExecuteUpdate(s => s
                .SetProperty(a => a.CourtRecordFlag, hasFlag)
                .SetProperty(a => a.CourtRecordUrl, url)
                .SetProperty(a => a.CourtRecordDate, date)
                .SetProperty(a => a.CourtRecordSummary, summary)
                .SetProperty(a => a.CourtRecordEnrichedAt, enrichedAt)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow));
    }

    public void UpdateFirmAdvisorCountChange(int firmId, int? delta)
    {
        using var ctx = CreateContext();
        ctx.Firms
            .Where(f => f.Id == firmId)
            .ExecuteUpdate(s => s
                .SetProperty(f => f.AdvisorCountChange1Yr, delta));
    }

    // ── Disclosure Scoring ────────────────────────────────────────────────

    public void UpdateDisclosureSeverityScore(int advisorId, int score)
    {
        using var ctx = CreateContext();
        ctx.Advisors
            .Where(a => a.Id == advisorId)
            .ExecuteUpdate(s => s
                .SetProperty(a => a.DisclosureSeverityScore, score));
    }

    // ── Form ADV Deep Enrichment ───────────────────────────────────────────

    public void UpdateFirmEnrichmentData(Firm firm)
    {
        using var ctx = CreateContext();
        ctx.Firms
            .Where(f => f.Id == firm.Id)
            .ExecuteUpdate(s => s
                .SetProperty(f => f.InvestmentStrategies, firm.InvestmentStrategies)
                .SetProperty(f => f.WrapFeePrograms, firm.WrapFeePrograms)
                .SetProperty(f => f.CryptoExposure, firm.CryptoExposure)
                .SetProperty(f => f.DirectIndexing, firm.DirectIndexing)
                .SetProperty(f => f.IsDuallyRegistered, firm.IsDuallyRegistered)
                .SetProperty(f => f.OwnershipStructure, firm.OwnershipStructure)
                .SetProperty(f => f.FormAdvDeepEnrichedAt, firm.FormAdvDeepEnrichedAt)
                .SetProperty(f => f.UpdatedAt, DateTime.UtcNow));
    }

    public void UpdateMobilityScore(int advisorId, int score, DateTime updatedAt)
    {
        using var ctx = CreateContext();
        ctx.Advisors
            .Where(a => a.Id == advisorId)
            .ExecuteUpdate(s => s
                .SetProperty(a => a.MobilityScore, score)
                .SetProperty(a => a.MobilityScoreUpdatedAt, updatedAt));
    }
}
