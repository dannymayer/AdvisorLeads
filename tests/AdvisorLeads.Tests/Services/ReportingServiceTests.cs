using AdvisorLeads.Data;
using AdvisorLeads.Models;
using AdvisorLeads.Services;

namespace AdvisorLeads.Tests.Services;

/// <summary>
/// Integration tests for ReportingService using a real per-test SQLite database.
/// Each test gets a fresh database to ensure isolation.
/// </summary>
public class ReportingServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ReportingService _svc;
    private readonly AdvisorRepository _advisorRepo;
    private readonly DatabaseContext _ctx;

    public ReportingServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"reporting_test_{Guid.NewGuid():N}.db");
        _ctx = new DatabaseContext(_dbPath);
        _ctx.InitializeDatabase();
        _advisorRepo = new AdvisorRepository(_dbPath);
        _svc = new ReportingService(_dbPath);
    }

    public void Dispose()
    {
        _ctx.Dispose();
        try { File.Delete(_dbPath); } catch { }
    }

    // ── Seed helpers ──────────────────────────────────────────────────────

    private Firm SeedFirm(string crd, string name, decimal? aum = null, int? numAdvisors = null,
        bool brokerProtocol = false, bool? feeOnly = null, bool? commission = null,
        int? hnwClients = null, int? numClients = null, int? priorAdvisorCount = null)
    {
        var firm = new Firm
        {
            CrdNumber = crd,
            Name = name,
            RegulatoryAum = aum,
            NumberOfAdvisors = numAdvisors,
            BrokerProtocolMember = brokerProtocol,
            CompensationFeeOnly = feeOnly,
            CompensationCommission = commission,
            ClientsHighNetWorth = hnwClients,
            NumClients = numClients,
            PriorAdvisorCount = priorAdvisorCount,
        };
        _ctx.Firms.Add(firm);
        _ctx.SaveChanges();
        return firm;
    }

    private Advisor SeedAdvisor(string crd, string first, string last, string state = "CA",
        string status = "Active", int? yearsExp = 5, bool hasDis = false,
        bool isFav = false, bool imported = false, string? email = null,
        int? firmId = null, string? firmCrd = null, string? firmName = null,
        int? totalFirmCount = null, DateTime? currentFirmStartDate = null,
        DateTime? careerStartDate = null, int? disclosureCount = 0,
        bool hasCriminal = false, bool hasRegulatory = false)
    {
        var advisor = new Advisor
        {
            CrdNumber = crd,
            FirstName = first,
            LastName = last,
            State = state,
            RegistrationStatus = status,
            YearsOfExperience = yearsExp,
            HasDisclosures = hasDis,
            IsFavorited = isFav,
            IsImportedToCrm = imported,
            Email = email,
            CurrentFirmId = firmId,
            CurrentFirmCrd = firmCrd,
            CurrentFirmName = firmName,
            TotalFirmCount = totalFirmCount,
            CurrentFirmStartDate = currentFirmStartDate,
            CareerStartDate = careerStartDate,
            DisclosureCount = disclosureCount ?? 0,
            HasCriminalDisclosure = hasCriminal,
            HasRegulatoryDisclosure = hasRegulatory,
        };
        _advisorRepo.UpsertAdvisor(advisor);
        return _ctx.Advisors.First(a => a.CrdNumber == crd);
    }

    // ── Tests ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFlightRiskAsync_WithActiveAdvisors_ReturnsSortedByScore()
    {
        var firm = SeedFirm("F001", "Alpha Wealth", aum: 500_000_000m, numAdvisors: 10,
            brokerProtocol: true, priorAdvisorCount: 15); // declining headcount → +20

        // High flight risk: many firms, broker protocol member, long tenure, high firm count
        SeedAdvisor("A001", "High", "Risk", firmId: firm.Id, firmCrd: firm.CrdNumber,
            firmName: firm.Name, totalFirmCount: 5,
            currentFirmStartDate: DateTime.Today.AddYears(-12), yearsExp: 15); // tenure >10 →+20, firms>=4 →+15, BP →+10

        // Low flight risk: few firms, short tenure
        SeedAdvisor("A002", "Low", "Risk", firmId: firm.Id, firmCrd: firm.CrdNumber,
            firmName: firm.Name, totalFirmCount: 2,
            currentFirmStartDate: DateTime.Today.AddYears(-1), yearsExp: 5);

        var filter = new ReportFilter { ActiveOnly = true, ExcludeExcluded = true };
        var results = await _svc.GetFlightRiskAsync(filter);

        Assert.NotEmpty(results);
        // Scores should be descending
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].FlightRiskScore >= results[i].FlightRiskScore);

        // High-risk advisor should appear before low-risk
        var highIdx = results.FindIndex(r => r.CrdNumber == "A001");
        var lowIdx = results.FindIndex(r => r.CrdNumber == "A002");
        Assert.True(highIdx <= lowIdx, "High-risk advisor should have higher score than low-risk");
    }

    [Fact]
    public async Task GetHighValueTargets_WithFirmAum_ComputesAumPerAdvisor()
    {
        var firm = SeedFirm("F002", "Big AUM Firm", aum: 1_000_000_000m, numAdvisors: 10);
        SeedAdvisor("B001", "Wealthy", "Advisor", firmId: firm.Id, firmCrd: firm.CrdNumber,
            firmName: firm.Name, yearsExp: 15, hasDis: false);

        var results = await _svc.GetHighValueTargetsAsync(new ReportFilter());

        Assert.NotEmpty(results);
        var row = results.First(r => r.CrdNumber == "B001");
        Assert.Equal(100_000_000m, row.AumPerAdvisor); // 1B / 10
        // 15 years experience → +20, no disclosures → +20
        Assert.True(row.TargetScore >= 40);
    }

    [Fact]
    public async Task GetTenureDistribution_ReturnsCorrectBuckets()
    {
        var firm = SeedFirm("F003", "Tenure Firm", aum: 100_000_000m, numAdvisors: 3);

        SeedAdvisor("T001", "Short", "Tenure", firmId: firm.Id,
            currentFirmStartDate: DateTime.Today.AddMonths(-6)); // <2 years

        SeedAdvisor("T002", "Mid", "Tenure", firmId: firm.Id,
            currentFirmStartDate: DateTime.Today.AddYears(-3)); // 2-5 years

        SeedAdvisor("T003", "Long", "Tenure", firmId: firm.Id,
            currentFirmStartDate: DateTime.Today.AddYears(-15)); // 10-20 years

        var results = await _svc.GetTenureDistributionSummaryAsync(new ReportFilter());

        Assert.NotEmpty(results);
        var buckets = results.Select(r => r.TenureBucket).ToList();
        Assert.Contains("<2 Years", buckets);
        Assert.Contains("2-5 Years", buckets);

        var shortBucket = results.First(r => r.TenureBucket == "<2 Years");
        Assert.True(shortBucket.AdvisorCount >= 1);
    }

    [Fact]
    public async Task GetBrokerProtocolDirectory_ReturnsOnlyBPMembers()
    {
        SeedFirm("BP01", "BP Member Firm", numAdvisors: 20, brokerProtocol: true);
        SeedFirm("BP02", "Non-BP Firm", numAdvisors: 15, brokerProtocol: false);

        var results = await _svc.GetBrokerProtocolDirectoryAsync(new ReportFilter());

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("BP01", r.CrdNumber));
        Assert.DoesNotContain(results, r => r.CrdNumber == "BP02");
    }

    [Fact]
    public async Task GetDisclosureProfile_ComputesRatesCorrectly()
    {
        SeedAdvisor("D001", "Clean", "Advisor", hasDis: false);
        SeedAdvisor("D002", "Dirty", "Advisor", hasDis: true, disclosureCount: 2,
            hasCriminal: true, hasRegulatory: true);
        SeedAdvisor("D003", "Also", "Clean", hasDis: false);

        var summary = await _svc.GetDisclosureProfileSummaryAsync(new ReportFilter());

        Assert.NotNull(summary);
        Assert.Equal(3, summary!.TotalAdvisors);
        Assert.Equal(1, summary.AdvisorsWithDisclosures);
        Assert.True(summary.DisclosureRate > 0 && summary.DisclosureRate < 1);
        Assert.Equal(1, summary.CriminalCount);
        Assert.Equal(1, summary.RegulatoryCount);
    }

    [Fact]
    public async Task GetPipelineFunnel_CountsFavoritedAndImported()
    {
        SeedAdvisor("P001", "Active", "One");
        SeedAdvisor("P002", "Fav", "Two");
        SeedAdvisor("P003", "FavEmail", "Three", email: "a@b.com");
        SeedAdvisor("P004", "Imported", "Four", email: "b@c.com");

        // UpsertAdvisor deliberately skips IsFavorited/IsImportedToCrm (CRM-managed flags).
        // Use a direct connection so the changes are visibly committed before the repo query runs.
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Advisors SET IsFavorited = 1 WHERE CrdNumber IN ('P002','P003','P004')";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "UPDATE Advisors SET IsImportedToCrm = 1 WHERE CrdNumber = 'P004'";
            cmd.ExecuteNonQuery();
        }

        var summary = await _svc.GetPipelineFunnelSummaryAsync(new ReportFilter());

        Assert.NotNull(summary);
        Assert.Equal(4, summary!.TotalActive);
        Assert.Equal(3, summary.Favorited);           // P002, P003, P004
        Assert.Equal(2, summary.FavoritedWithEmail);  // P003, P004
        Assert.Equal(1, summary.ImportedToCrm);       // P004
        Assert.Equal(1, summary.EnrichmentGap);       // 3 favorited - 2 with email
    }

    [Fact]
    public async Task GetGeographicDensity_GroupsByState()
    {
        SeedAdvisor("G001", "Alice", "One", state: "CA");
        SeedAdvisor("G002", "Bob", "Two", state: "CA");
        SeedAdvisor("G003", "Carol", "Three", state: "TX");

        var results = await _svc.GetGeographicDensityAsync(new ReportFilter { ActiveOnly = false });

        Assert.NotEmpty(results);
        var ca = results.FirstOrDefault(r => r.State == "CA");
        var tx = results.FirstOrDefault(r => r.State == "TX");
        Assert.NotNull(ca);
        Assert.NotNull(tx);
        Assert.Equal(2, ca!.AdvisorCount);
        Assert.Equal(1, tx!.AdvisorCount);
        // CA has more advisors, should appear first
        Assert.True(results.IndexOf(ca) < results.IndexOf(tx));
    }

    [Fact]
    public async Task GetCompensationAnalysis_DerivesModelLabels()
    {
        SeedFirm("C001", "Fee-Only Firm", feeOnly: true, commission: false);
        SeedFirm("C002", "Commission Firm", feeOnly: false, commission: true);
        SeedFirm("C003", "Both Firm", feeOnly: true, commission: true);

        var results = await _svc.GetCompensationAnalysisAsync(new ReportFilter());

        Assert.NotEmpty(results);
        var labels = results.Select(r => r.CompensationModel).ToList();
        Assert.Contains("Fee-Only", labels);
        Assert.Contains("Commission-Only", labels);
        Assert.Contains("Fee & Commission", labels);
    }

    [Fact]
    public void ReportFilter_DefaultsAreCorrect()
    {
        var filter = new ReportFilter();

        Assert.True(filter.ActiveOnly);
        Assert.True(filter.ExcludeExcluded);
        Assert.Equal(1000, filter.PageSize);
        Assert.Null(filter.State);
        Assert.Null(filter.RecordType);
        Assert.Null(filter.FirmCrd);
        Assert.False(filter.BrokerProtocolOnly);
        Assert.False(filter.NoDisclosuresOnly);
        Assert.False(filter.FavoritedOnly);
        Assert.Null(filter.MinYearsExperience);
        Assert.Null(filter.MinRegulatoryAum);
        Assert.Null(filter.CareerStartedAfter);
    }
}
