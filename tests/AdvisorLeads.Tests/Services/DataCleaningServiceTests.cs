using AdvisorLeads.Data;
using AdvisorLeads.Models;
using AdvisorLeads.Services;
using Microsoft.EntityFrameworkCore;

namespace AdvisorLeads.Tests.Services;

public class DataCleaningServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DataCleaningService _service;

    public DataCleaningServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"advisorleads_clean_test_{Guid.NewGuid():N}.db");
        using var ctx = new DatabaseContext(_dbPath);
        ctx.InitializeDatabase();
        _service = new DataCleaningService(_dbPath);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    private void SeedAdvisor(string crd, string first, string last, string? state = null,
        string? firmCrd = null, string? firmName = null, string? email = null,
        string? phone = null, string? recordType = null, string? regStatus = null,
        int? yearsExp = null, DateTime? regDate = null, string? source = null)
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.Advisors.Add(new Advisor
        {
            CrdNumber = crd,
            FirstName = first,
            LastName = last,
            State = state,
            CurrentFirmCrd = firmCrd,
            CurrentFirmName = firmName,
            Email = email,
            Phone = phone,
            RecordType = recordType ?? "Registered Representative",
            RegistrationStatus = regStatus ?? "Active",
            YearsOfExperience = yearsExp,
            RegistrationDate = regDate,
            Source = source ?? "FINRA",
            UpdatedAt = DateTime.UtcNow
        });
        ctx.SaveChanges();
    }

    private void SeedFirm(string crd, string name, string? state = null, DateTime? regDate = null, decimal? aum = null)
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.Firms.Add(new Firm
        {
            CrdNumber = crd,
            Name = name,
            State = state,
            RegistrationDate = regDate,
            RegulatoryAum = aum,
            RecordType = "Investment Adviser",
            UpdatedAt = DateTime.UtcNow
        });
        ctx.SaveChanges();
    }

    private void SeedEmploymentHistory(int advisorId, string firmName)
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.EmploymentHistory.Add(new EmploymentHistory
        {
            AdvisorId = advisorId,
            FirmName = firmName
        });
        ctx.SaveChanges();
    }

    private void SeedDisclosure(int advisorId, string type)
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.Disclosures.Add(new Disclosure
        {
            AdvisorId = advisorId,
            Type = type
        });
        ctx.SaveChanges();
    }

    private void SeedQualification(int advisorId, string name)
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.Qualifications.Add(new Qualification
        {
            AdvisorId = advisorId,
            Name = name
        });
        ctx.SaveChanges();
    }

    private int GetAdvisorId(string crd)
    {
        using var ctx = new DatabaseContext(_dbPath);
        return ctx.Advisors.First(a => a.CrdNumber == crd).Id;
    }

    // ── Duplicate Detection Tests ─────────────────────────────────────

    [Fact]
    public async Task FindDuplicateAdvisors_WithSameNameFirmState_ReturnsGroup()
    {
        SeedFirm("F100", "Test Firm", "CA");
        SeedAdvisor("100", "John", "Smith", "CA", "F100", "Test Firm", source: "FINRA");
        SeedAdvisor("200", "John", "Smith", "CA", "F100", "Test Firm", source: "SEC");

        var groups = await _service.FindDuplicateAdvisorsAsync(CancellationToken.None);

        Assert.Single(groups);
        Assert.Equal(DuplicateReason.NameFirmStateMatch, groups[0].Reason);
        Assert.Equal(2, groups[0].EntityIds.Count);
    }

    [Fact]
    public async Task FindDuplicateAdvisors_WithUniqueAdvisors_ReturnsEmpty()
    {
        SeedAdvisor("100", "John", "Smith", "CA");
        SeedAdvisor("200", "Jane", "Doe", "TX");
        SeedAdvisor("300", "Bob", "Builder", "NY");

        var groups = await _service.FindDuplicateAdvisorsAsync(CancellationToken.None);

        Assert.Empty(groups);
    }

    [Fact]
    public async Task FindDuplicateAdvisors_WithSameNameStateRegDate_ReturnsGroup()
    {
        var regDate = new DateTime(2010, 5, 15);
        SeedAdvisor("100", "Alice", "Wonder", "NY", regDate: regDate);
        SeedAdvisor(null!, "Alice", "Wonder", "NY", regDate: regDate);

        var groups = await _service.FindDuplicateAdvisorsAsync(CancellationToken.None);

        Assert.Single(groups);
        Assert.Equal(DuplicateReason.NameRegistrationDateMatch, groups[0].Reason);
    }

    [Fact]
    public async Task FindDuplicateFirms_WithSameNameStateRegDate_ReturnsGroup()
    {
        var regDate = new DateTime(2005, 1, 1);
        SeedFirm("F1", "Acme Advisors", "CA", regDate);
        SeedFirm("F2", "Acme Advisors", "CA", regDate);

        var groups = await _service.FindDuplicateFirmsAsync(CancellationToken.None);

        Assert.Single(groups);
        Assert.Equal(2, groups[0].EntityIds.Count);
    }

    // ── Normalization Tests ───────────────────────────────────────────

    [Fact]
    public async Task FindNormalizationIssues_WithAllCapsName_ReturnsTitleCaseIssue()
    {
        SeedAdvisor("NC01", "JOHN", "SMITH");

        var issues = await _service.FindNormalizationIssuesAsync(CancellationToken.None, null);

        var nameIssues = issues.Where(i =>
            i.EntityId == GetAdvisorId("NC01")
            && i.FieldName is "FirstName" or "LastName"
            && i.Type == CleaningIssueType.NormalizationNeeded).ToList();

        Assert.Equal(2, nameIssues.Count);
        Assert.Contains(nameIssues, i => i.SuggestedValue == "John");
        Assert.Contains(nameIssues, i => i.SuggestedValue == "Smith");
    }

    [Fact]
    public async Task FindNormalizationIssues_WithInvalidState_ReturnsNormalizationIssue()
    {
        SeedAdvisor("NS01", "Test", "User", state: "California");

        var issues = await _service.FindNormalizationIssuesAsync(CancellationToken.None, null);

        var stateIssue = issues.FirstOrDefault(i =>
            i.EntityId == GetAdvisorId("NS01") && i.FieldName == "State");
        Assert.NotNull(stateIssue);
        Assert.Equal("CA", stateIssue!.SuggestedValue);
        Assert.True(stateIssue.IsAutoFixable);
    }

    [Fact]
    public async Task FindNormalizationIssues_WithLowercaseState_ReturnsNormalizationIssue()
    {
        SeedAdvisor("NS02", "Test", "User", state: "ca");

        var issues = await _service.FindNormalizationIssuesAsync(CancellationToken.None, null);

        var stateIssue = issues.FirstOrDefault(i =>
            i.EntityId == GetAdvisorId("NS02") && i.FieldName == "State");
        Assert.NotNull(stateIssue);
        Assert.Equal("CA", stateIssue!.SuggestedValue);
    }

    [Fact]
    public async Task FindNormalizationIssues_WithNegativeYearsExperience_ReturnsImplausibleValueIssue()
    {
        SeedAdvisor("YE01", "Test", "User", yearsExp: -5);

        var issues = await _service.FindNormalizationIssuesAsync(CancellationToken.None, null);

        var expIssue = issues.FirstOrDefault(i =>
            i.EntityId == GetAdvisorId("YE01") && i.FieldName == "YearsOfExperience");
        Assert.NotNull(expIssue);
        Assert.Equal(CleaningIssueType.ImplausibleValue, expIssue!.Type);
    }

    [Fact]
    public async Task FindNormalizationIssues_WithHighYearsExperience_ReturnsImplausibleValueIssue()
    {
        SeedAdvisor("YE02", "Test", "User", yearsExp: 70);

        var issues = await _service.FindNormalizationIssuesAsync(CancellationToken.None, null);

        var expIssue = issues.FirstOrDefault(i =>
            i.EntityId == GetAdvisorId("YE02") && i.FieldName == "YearsOfExperience");
        Assert.NotNull(expIssue);
        Assert.Equal(CleaningIssueType.ImplausibleValue, expIssue!.Type);
    }

    [Fact]
    public async Task FindNormalizationIssues_WithFutureRegistrationDate_ReturnsImplausibleValueIssue()
    {
        SeedAdvisor("RD01", "Test", "User", regDate: DateTime.UtcNow.AddYears(1));

        var issues = await _service.FindNormalizationIssuesAsync(CancellationToken.None, null);

        var dateIssue = issues.FirstOrDefault(i =>
            i.EntityId == GetAdvisorId("RD01") && i.FieldName == "RegistrationDate");
        Assert.NotNull(dateIssue);
        Assert.Equal(CleaningIssueType.ImplausibleValue, dateIssue!.Type);
        Assert.Equal(CleaningIssueSeverity.High, dateIssue.Severity);
    }

    [Fact]
    public async Task FindNormalizationIssues_WithUnknownRecordType_ReturnsIssue()
    {
        SeedAdvisor("RT01", "Test", "User", recordType: "Unknown Type");

        var issues = await _service.FindNormalizationIssuesAsync(CancellationToken.None, null);

        var typeIssue = issues.FirstOrDefault(i =>
            i.EntityId == GetAdvisorId("RT01") && i.FieldName == "RecordType");
        Assert.NotNull(typeIssue);
        Assert.Equal(CleaningIssueType.UnknownEnumValue, typeIssue!.Type);
    }

    [Fact]
    public async Task FindNormalizationIssues_WithPhoneDigits_ReturnFormattedPhone()
    {
        SeedAdvisor("PH01", "Test", "User", phone: "5551234567");

        var issues = await _service.FindNormalizationIssuesAsync(CancellationToken.None, null);

        var phoneIssue = issues.FirstOrDefault(i =>
            i.EntityId == GetAdvisorId("PH01") && i.FieldName == "Phone");
        Assert.NotNull(phoneIssue);
        Assert.Equal("(555) 123-4567", phoneIssue!.SuggestedValue);
    }

    [Fact]
    public async Task FindNormalizationIssues_WithEmailCasing_ReturnsLowercaseIssue()
    {
        SeedAdvisor("EM01", "Test", "User", email: "Test@Example.COM ");

        var issues = await _service.FindNormalizationIssuesAsync(CancellationToken.None, null);

        var emailIssue = issues.FirstOrDefault(i =>
            i.EntityId == GetAdvisorId("EM01") && i.FieldName == "Email");
        Assert.NotNull(emailIssue);
        Assert.Equal("test@example.com", emailIssue!.SuggestedValue);
    }

    // ── Orphaned Records Tests ────────────────────────────────────────

    [Fact]
    public async Task FindOrphanedRecords_WithOrphanedEmploymentHistory_ReturnsIssue()
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
        ctx.Database.ExecuteSqlRaw(
            "INSERT INTO EmploymentHistory (AdvisorId, FirmName) VALUES (99999, 'Ghost Firm')");
        ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON");

        var issues = await _service.FindOrphanedRecordsAsync(CancellationToken.None);

        Assert.Contains(issues, i =>
            i.EntityType == CleaningEntityType.EmploymentHistory
            && i.Description.Contains("99999"));
    }

    [Fact]
    public async Task FindOrphanedRecords_WithOrphanedDisclosure_ReturnsIssue()
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
        ctx.Database.ExecuteSqlRaw(
            "INSERT INTO Disclosures (AdvisorId, Type) VALUES (99998, 'Criminal')");
        ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON");

        var issues = await _service.FindOrphanedRecordsAsync(CancellationToken.None);

        Assert.Contains(issues, i =>
            i.EntityType == CleaningEntityType.Disclosure
            && i.Description.Contains("99998"));
    }

    [Fact]
    public async Task FindOrphanedRecords_WithNoOrphans_ReturnsEmpty()
    {
        SeedAdvisor("OR01", "Test", "User");
        int id = GetAdvisorId("OR01");
        SeedEmploymentHistory(id, "Legit Firm");
        SeedQualification(id, "Series 7");

        var issues = await _service.FindOrphanedRecordsAsync(CancellationToken.None);

        Assert.Empty(issues);
    }

    // ── Fix Method Tests ──────────────────────────────────────────────

    [Fact]
    public async Task ApplyNormalizations_WithNameCasingIssue_UpdatesDatabase()
    {
        SeedAdvisor("FIX01", "JANE", "DOE");
        int id = GetAdvisorId("FIX01");

        var issues = new List<CleaningIssue>
        {
            new()
            {
                Type = CleaningIssueType.NormalizationNeeded,
                EntityType = CleaningEntityType.Advisor,
                EntityId = id,
                FieldName = "FirstName",
                CurrentValue = "JANE",
                SuggestedValue = "Jane",
                IsAutoFixable = true
            },
            new()
            {
                Type = CleaningIssueType.NormalizationNeeded,
                EntityType = CleaningEntityType.Advisor,
                EntityId = id,
                FieldName = "LastName",
                CurrentValue = "DOE",
                SuggestedValue = "Doe",
                IsAutoFixable = true
            }
        };

        int count = await _service.ApplyNormalizationsAsync(issues, CancellationToken.None, null);

        Assert.Equal(2, count);
        using var ctx = new DatabaseContext(_dbPath);
        var advisor = ctx.Advisors.First(a => a.CrdNumber == "FIX01");
        Assert.Equal("Jane", advisor.FirstName);
        Assert.Equal("Doe", advisor.LastName);
    }

    [Fact]
    public async Task ApplyNormalizations_WithStateIssue_UpdatesDatabase()
    {
        SeedAdvisor("FIX02", "Test", "User", state: "California");
        int id = GetAdvisorId("FIX02");

        var issues = new List<CleaningIssue>
        {
            new()
            {
                Type = CleaningIssueType.NormalizationNeeded,
                EntityType = CleaningEntityType.Advisor,
                EntityId = id,
                FieldName = "State",
                CurrentValue = "California",
                SuggestedValue = "CA",
                IsAutoFixable = true
            }
        };

        int count = await _service.ApplyNormalizationsAsync(issues, CancellationToken.None, null);

        Assert.Equal(1, count);
        using var ctx = new DatabaseContext(_dbPath);
        var advisor = ctx.Advisors.First(a => a.CrdNumber == "FIX02");
        Assert.Equal("CA", advisor.State);
    }

    [Fact]
    public async Task DeleteOrphanedRecords_RemovesOrphanedEmploymentHistory()
    {
        using (var ctx = new DatabaseContext(_dbPath))
        {
            ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
            ctx.Database.ExecuteSqlRaw(
                "INSERT INTO EmploymentHistory (AdvisorId, FirmName) VALUES (88888, 'Orphan Firm')");
            ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON");
        }

        var issues = await _service.FindOrphanedRecordsAsync(CancellationToken.None);
        Assert.NotEmpty(issues);

        int deleted = await _service.DeleteOrphanedRecordsAsync(issues, CancellationToken.None);

        Assert.True(deleted > 0);

        var remaining = await _service.FindOrphanedRecordsAsync(CancellationToken.None);
        Assert.Empty(remaining);
    }

    // ── Merge Tests ───────────────────────────────────────────────────

    [Fact]
    public async Task MergeDuplicateAdvisors_WithTwoRecords_KeepsOneAndMovesRelatedData()
    {
        SeedAdvisor("MRG01", "John", "Merge", "CA", "F100");
        SeedAdvisor("MRG02", "John", "Merge", "CA", "F100");
        int keepId = GetAdvisorId("MRG01");
        int deleteId = GetAdvisorId("MRG02");

        SeedEmploymentHistory(deleteId, "Old Firm");
        SeedDisclosure(deleteId, "Criminal");
        SeedQualification(deleteId, "Series 66");

        var group = new DuplicateGroup
        {
            EntityIds = new List<int> { keepId, deleteId },
            EntityType = CleaningEntityType.Advisor,
            Reason = DuplicateReason.NameFirmStateMatch,
            SuggestedKeepId = keepId
        };

        await _service.MergeDuplicateAdvisorsAsync(group, keepId, CancellationToken.None);

        using var ctx = new DatabaseContext(_dbPath);
        Assert.Null(ctx.Advisors.FirstOrDefault(a => a.Id == deleteId));
        Assert.NotNull(ctx.Advisors.FirstOrDefault(a => a.Id == keepId));

        var employment = ctx.EmploymentHistory.Where(eh => eh.AdvisorId == keepId).ToList();
        Assert.Contains(employment, eh => eh.FirmName == "Old Firm");

        var disclosures = ctx.Disclosures.Where(d => d.AdvisorId == keepId).ToList();
        Assert.Contains(disclosures, d => d.Type == "Criminal");

        var qualifications = ctx.Qualifications.Where(q => q.AdvisorId == keepId).ToList();
        Assert.Contains(qualifications, q => q.Name == "Series 66");
    }

    // ── Inconsistent Relationships Tests ──────────────────────────────

    [Fact]
    public async Task FindInconsistentRelationships_WithNonExistentFirmCrd_ReturnsIssue()
    {
        SeedAdvisor("IR01", "Test", "User", firmCrd: "NONEXISTENT");

        var issues = await _service.FindInconsistentRelationshipsAsync(CancellationToken.None);

        Assert.Contains(issues, i =>
            i.EntityId == GetAdvisorId("IR01")
            && i.FieldName == "CurrentFirmCrd"
            && i.Type == CleaningIssueType.InconsistentRelationship);
    }

    [Fact]
    public async Task FindInconsistentRelationships_WithFirmNameMismatch_ReturnsIssue()
    {
        SeedFirm("F200", "Correct Firm Name", "NY");
        SeedAdvisor("IR02", "Test", "User", firmCrd: "F200", firmName: "Wrong Firm Name");

        var issues = await _service.FindInconsistentRelationshipsAsync(CancellationToken.None);

        var mismatch = issues.FirstOrDefault(i =>
            i.EntityId == GetAdvisorId("IR02")
            && i.FieldName == "CurrentFirmName");
        Assert.NotNull(mismatch);
        Assert.Equal("Correct Firm Name", mismatch!.SuggestedValue);
    }

    // ── Full Analysis Test ────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_WithMixedDataQuality_ReturnsCompleteReport()
    {
        // Seed some duplicates
        SeedFirm("FA01", "Analysis Firm", "CA");
        SeedAdvisor("A01", "John", "Smith", "CA", "FA01", "Analysis Firm");
        SeedAdvisor("A02", "John", "Smith", "CA", "FA01", "Analysis Firm");

        // Seed some normalization issues
        SeedAdvisor("A03", "JANE", "DOE", state: "California", yearsExp: -3);

        // Seed an orphan
        using (var ctx = new DatabaseContext(_dbPath))
        {
            ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
            ctx.Database.ExecuteSqlRaw(
                "INSERT INTO EmploymentHistory (AdvisorId, FirmName) VALUES (77777, 'Orphan')");
            ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON");
        }

        // Seed an inconsistency
        SeedAdvisor("A04", "Bob", "Builder", firmCrd: "MISSING_FIRM");

        var report = await _service.AnalyzeAsync(CancellationToken.None, null);

        Assert.NotNull(report);
        Assert.True(report.TotalAdvisors >= 4);
        Assert.True(report.TotalFirms >= 1);
        Assert.True(report.TotalIssues > 0);
        Assert.True(report.DuplicateAdvisorGroupCount > 0);
        Assert.True(report.NormalizationIssueCount > 0);
        Assert.True(report.OrphanedRecordCount > 0);
        Assert.True(report.InconsistentRelationshipCount > 0);
        Assert.True(report.AnalysisDuration.TotalMilliseconds > 0);
    }
}
