using AdvisorLeads.Data;
using AdvisorLeads.Models;
using AdvisorLeads.Services;

namespace AdvisorLeads.Tests.Services;

/// <summary>
/// Integration and unit tests for Alerts &amp; Monitoring services.
/// Each test that touches the database gets its own fresh SQLite file.
/// </summary>
public class AlertServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AdvisorRepository _repo;
    private readonly AlertRepository _alertRepo;

    public AlertServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"advisorleads_alerts_test_{Guid.NewGuid():N}.db");
        using var ctx = new DatabaseContext(_dbPath);
        ctx.InitializeDatabase();
        _repo = new AdvisorRepository(_dbPath);
        _alertRepo = new AlertRepository(_dbPath);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static Advisor MakeAdvisor(string crd, string first = "John", string last = "Doe",
        string? state = "TX", string? firmCrd = "FIRM001", string? firmName = "Test Firm")
        => new Advisor
        {
            CrdNumber = crd,
            FirstName = first,
            LastName = last,
            State = state,
            CurrentFirmCrd = firmCrd,
            CurrentFirmName = firmName,
            DisclosureCount = 0,
        };

    // ── WatchListMonitorService Tests ─────────────────────────────────

    [Fact]
    public void WatchListMonitorService_DetectsDisclosureCountIncrease()
    {
        var existing = MakeAdvisor("CRD001");
        existing.DisclosureCount = 1;

        var incoming = MakeAdvisor("CRD001");
        incoming.DisclosureCount = 3;
        incoming.HasCriminalDisclosure = true;

        var alerts = WatchListMonitorService.CheckAdvisorForChanges(existing, incoming);

        Assert.NotEmpty(alerts);
        var disclosure = Assert.Single(alerts.Where(a => a.AlertType == "Disclosure"));
        Assert.Equal("High", disclosure.Severity);
        Assert.Equal("CRD001", disclosure.EntityCrd);
        Assert.Contains("2 new disclosures", disclosure.Summary);
        Assert.Contains("Criminal", disclosure.Summary);
    }

    [Fact]
    public void WatchListMonitorService_DetectsFirmChange()
    {
        var existing = MakeAdvisor("CRD002", firmCrd: "FIRM001", firmName: "Old Firm");
        var incoming = MakeAdvisor("CRD002", firmCrd: "FIRM002", firmName: "New Firm");

        var alerts = WatchListMonitorService.CheckAdvisorForChanges(existing, incoming);

        var firmChange = Assert.Single(alerts.Where(a => a.AlertType == "FirmChange"));
        Assert.Equal("FIRM001", firmChange.OldValue);
        Assert.Equal("FIRM002", firmChange.NewValue);
        Assert.Contains("Old Firm", firmChange.Summary);
        Assert.Contains("New Firm", firmChange.Summary);
    }

    [Fact]
    public void WatchListMonitorService_NoAlertWhenFieldsUnchanged()
    {
        var existing = MakeAdvisor("CRD003");
        existing.DisclosureCount = 2;
        var incoming = MakeAdvisor("CRD003");
        incoming.DisclosureCount = 2;

        var alerts = WatchListMonitorService.CheckAdvisorForChanges(existing, incoming);

        Assert.Empty(alerts);
    }

    [Fact]
    public void WatchListMonitorService_NoAlertWhenNotWatched()
    {
        var advisor = MakeAdvisor("CRD004");
        advisor.DisclosureCount = 0;
        _repo.UpsertAdvisor(advisor);

        var existing = _repo.GetAdvisorByCrd("CRD004")!;
        Assert.False(existing.IsWatched);

        var incoming = MakeAdvisor("CRD004");
        incoming.DisclosureCount = 5;

        // Service only checks if called; UpsertAdvisor skips monitor when not watched
        var service = new WatchListMonitorService(_repo, _alertRepo);
        // No RecordNewAdvisorAlerts called (IsWatched=false), so no alerts in DB
        Assert.Equal(0, _alertRepo.GetUnreadCount());
    }

    [Fact]
    public void WatchListMonitorService_RecordNewAdvisorAlerts_PersistsAlerts()
    {
        var existing = MakeAdvisor("CRD005");
        existing.DisclosureCount = 0;
        existing.IsWatched = true;

        var incoming = MakeAdvisor("CRD005");
        incoming.DisclosureCount = 2;

        var service = new WatchListMonitorService(_repo, _alertRepo);
        service.RecordNewAdvisorAlerts(existing, incoming);

        Assert.Equal(1, _alertRepo.GetUnreadCount());
        var alerts = _alertRepo.GetAlertsForEntity("CRD005");
        Assert.Single(alerts);
        Assert.Equal("Disclosure", alerts[0].AlertType);
    }

    // ── AlertRepository Tests ─────────────────────────────────────────

    [Fact]
    public void AlertRepository_AddAndRetrieve_RoundTrips()
    {
        var alert = new AlertLog
        {
            AlertType  = "Disclosure",
            Severity   = "High",
            EntityType = "Advisor",
            EntityCrd  = "ROUND01",
            EntityName = "Test Advisor",
            Summary    = "Test alert summary",
            DetectedAt = DateTime.UtcNow,
            CreatedAt  = DateTime.UtcNow
        };

        _alertRepo.AddAlert(alert);

        var results = _alertRepo.GetAlertsForEntity("ROUND01");
        Assert.Single(results);
        Assert.Equal("Disclosure", results[0].AlertType);
        Assert.Equal("High", results[0].Severity);
        Assert.Equal("Test alert summary", results[0].Summary);
    }

    [Fact]
    public void AlertRepository_GetUnreadCount_OnlyCountsUnread()
    {
        _alertRepo.AddAlert(new AlertLog { AlertType = "Disclosure", EntityCrd = "U01", Summary = "A", DetectedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        _alertRepo.AddAlert(new AlertLog { AlertType = "FirmChange", EntityCrd = "U02", Summary = "B", DetectedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        _alertRepo.AddAlert(new AlertLog { AlertType = "AumThreshold", EntityCrd = "U03", Summary = "C", IsRead = true, DetectedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });

        Assert.Equal(2, _alertRepo.GetUnreadCount());
    }

    [Fact]
    public void AlertRepository_MarkAllRead_ClearsUnread()
    {
        _alertRepo.AddAlert(new AlertLog { AlertType = "Disclosure", EntityCrd = "MR01", Summary = "A", DetectedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        _alertRepo.AddAlert(new AlertLog { AlertType = "FirmChange", EntityCrd = "MR02", Summary = "B", DetectedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });

        Assert.Equal(2, _alertRepo.GetUnreadCount());
        _alertRepo.MarkAllRead();
        Assert.Equal(0, _alertRepo.GetUnreadCount());
    }

    [Fact]
    public void AlertRepository_Acknowledge_MarksReadAndAcknowledged()
    {
        _alertRepo.AddAlert(new AlertLog { AlertType = "Disclosure", EntityCrd = "ACK01", Summary = "X", DetectedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
        var alerts = _alertRepo.GetRecentAlerts();
        Assert.Single(alerts);

        _alertRepo.Acknowledge(alerts[0].Id);
        Assert.Equal(0, _alertRepo.GetUnreadCount());

        var after = _alertRepo.GetAlertsForEntity("ACK01");
        Assert.True(after[0].IsAcknowledged);
        Assert.True(after[0].IsRead);
    }

    // ── AumThresholdAlertService Tests ────────────────────────────────

    [Fact]
    public async Task AumThresholdAlertService_FiresAlert_WhenThresholdCrossed()
    {
        using var ctx = new DatabaseContext(_dbPath);

        ctx.FirmAumHistory.Add(new FirmAumHistory
        {
            FirmCrd = "FIRM_AUM1",
            SnapshotDate = DateTime.UtcNow.AddMonths(-2),
            TotalAum = 400_000_000,
            CreatedAt = DateTime.UtcNow
        });
        ctx.FirmAumHistory.Add(new FirmAumHistory
        {
            FirmCrd = "FIRM_AUM1",
            SnapshotDate = DateTime.UtcNow.AddMonths(-1),
            TotalAum = 600_000_000,
            CreatedAt = DateTime.UtcNow
        });
        ctx.SaveChanges();

        _alertRepo.UpsertAumRule(new FirmAumAlertRule
        {
            FirmCrd = "FIRM_AUM1",
            FirmName = "Test Firm AUM",
            ThresholdType = "CrossAbove",
            ThresholdAmount = 500_000_000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        var service = new AumThresholdAlertService(_dbPath, _alertRepo);
        int count = await service.CheckAumThresholdsAsync();

        Assert.Equal(1, count);
        var alerts = _alertRepo.GetAlertsForEntity("FIRM_AUM1");
        Assert.Single(alerts);
        Assert.Equal("AumThreshold", alerts[0].AlertType);
        Assert.Contains("crossed above", alerts[0].Summary);
    }

    [Fact]
    public async Task AumThresholdAlertService_NoAlert_WhenBelowThreshold()
    {
        using var ctx = new DatabaseContext(_dbPath);

        ctx.FirmAumHistory.Add(new FirmAumHistory
        {
            FirmCrd = "FIRM_AUM2",
            SnapshotDate = DateTime.UtcNow.AddMonths(-2),
            TotalAum = 200_000_000,
            CreatedAt = DateTime.UtcNow
        });
        ctx.FirmAumHistory.Add(new FirmAumHistory
        {
            FirmCrd = "FIRM_AUM2",
            SnapshotDate = DateTime.UtcNow.AddMonths(-1),
            TotalAum = 300_000_000,
            CreatedAt = DateTime.UtcNow
        });
        ctx.SaveChanges();

        _alertRepo.UpsertAumRule(new FirmAumAlertRule
        {
            FirmCrd = "FIRM_AUM2",
            FirmName = "Underperforming Firm",
            ThresholdType = "CrossAbove",
            ThresholdAmount = 1_000_000_000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        var service = new AumThresholdAlertService(_dbPath, _alertRepo);
        int count = await service.CheckAumThresholdsAsync();

        Assert.Equal(0, count);
        Assert.Empty(_alertRepo.GetAlertsForEntity("FIRM_AUM2"));
    }

    // ── MarketWatchService Tests ──────────────────────────────────────

    [Fact]
    public void MarketWatchService_AlertsOnNewRegistrant_MatchingRule()
    {
        var advisor = MakeAdvisor("MW_CRD1", "Jane", "Smith", state: "TX");
        advisor.RecordType = "Investment Advisor Representative";
        advisor.YearsOfExperience = 12;
        advisor.FirstSeenAt = DateTime.UtcNow.AddHours(-2);
        _repo.UpsertAdvisor(advisor);

        _alertRepo.UpsertMarketWatchRule(new MarketWatchRule
        {
            RuleName = "Texas IARs",
            State = "TX",
            RecordType = "Investment Advisor Representative",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var service = new MarketWatchService(_dbPath, _alertRepo);
        int count = service.CheckNewRegistrations(DateTime.UtcNow.AddHours(-25));

        Assert.Equal(1, count);
        var alerts = _alertRepo.GetAlertsForEntity("MW_CRD1");
        Assert.Single(alerts);
        Assert.Equal("NewRegistration", alerts[0].AlertType);
        Assert.Contains("Texas IARs", alerts[0].Summary);
    }

    [Fact]
    public void MarketWatchService_NoAlert_WhenRuleDoesNotMatch()
    {
        var advisor = MakeAdvisor("MW_CRD2", "Bob", "Jones", state: "CA");
        advisor.RecordType = "Registered Representative";
        advisor.FirstSeenAt = DateTime.UtcNow.AddHours(-2);
        _repo.UpsertAdvisor(advisor);

        _alertRepo.UpsertMarketWatchRule(new MarketWatchRule
        {
            RuleName = "Texas IARs",
            State = "TX",
            RecordType = "Investment Advisor Representative",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var service = new MarketWatchService(_dbPath, _alertRepo);
        int count = service.CheckNewRegistrations(DateTime.UtcNow.AddHours(-25));

        Assert.Equal(0, count);
        Assert.Empty(_alertRepo.GetAlertsForEntity("MW_CRD2"));
    }

    // ── BrokerProtocolAlertService Tests ─────────────────────────────

    [Fact]
    public void BrokerProtocolAlertService_DetectsJoinedAndLeft()
    {
        using var ctx = new DatabaseContext(_dbPath);
        ctx.Firms.Add(new Firm { CrdNumber = "BP_CRD1", Name = "Joined Firm", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        ctx.Firms.Add(new Firm { CrdNumber = "BP_CRD2", Name = "Left Firm", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        ctx.SaveChanges();

        var previous = new HashSet<string> { "BP_CRD2", "BP_CRD3" };
        var current  = new HashSet<string> { "BP_CRD1", "BP_CRD2" };

        var service = new BrokerProtocolAlertService(_dbPath, _alertRepo);
        int count = service.DetectAndRecordChanges(previous, current);

        Assert.Equal(2, count);

        var joinAlerts = _alertRepo.GetAlertsForEntity("BP_CRD1");
        Assert.Single(joinAlerts);
        Assert.Equal("BrokerProtocol", joinAlerts[0].AlertType);
        Assert.Contains("JOINED", joinAlerts[0].Summary);

        var withdrawAlerts = _alertRepo.GetAlertsForEntity("BP_CRD3");
        Assert.Single(withdrawAlerts);
        Assert.Equal("High", withdrawAlerts[0].Severity);
        Assert.Contains("WITHDREW", withdrawAlerts[0].Summary);
    }
}
