using AdvisorLeads.Controls;
using AdvisorLeads.Data;
using AdvisorLeads.Forms;
using AdvisorLeads.Models;

namespace AdvisorLeads.Tests.Controls;

public class AlertsPanelTests : IDisposable
{
    private readonly string _dbPath;

    public AlertsPanelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"alerts_test_{Guid.NewGuid():N}.db");
        using var ctx = new DatabaseContext(_dbPath);
        ctx.InitializeDatabase();
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    private AlertRepository CreateRepo() => new AlertRepository(_dbPath);

    [Fact]
    public void AlertsPanel_Constructor_DoesNotThrow()
    {
        var repo = CreateRepo();
        var panel = new AlertsPanel(
            repo,
            key => null,
            (key, value) => { });
        Assert.NotNull(panel);
        panel.Dispose();
    }

    [Fact]
    public void AlertRepository_MarkAsRead_UpdatesIsRead()
    {
        var repo = CreateRepo();
        var alert = new AlertLog
        {
            AlertType = "Disclosure",
            Severity = "High",
            EntityType = "Advisor",
            EntityCrd = "TEST001",
            Summary = "Test alert",
            IsRead = false,
            DetectedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        repo.AddAlert(alert);

        var before = repo.GetRecentAlerts(10);
        Assert.NotEmpty(before);
        int id = before[0].Id;
        Assert.False(before[0].IsRead);

        repo.MarkRead(id);

        var after = repo.GetRecentAlerts(10);
        var updated = after.First(a => a.Id == id);
        Assert.True(updated.IsRead);
    }

    [Fact]
    public void AlertRepository_GetUnreadCount_OnlyCountsUnread()
    {
        var repo = CreateRepo();

        repo.AddAlert(new AlertLog
        {
            AlertType = "FirmChange",
            Severity = "Medium",
            EntityType = "Firm",
            EntityCrd = "FIRM001",
            Summary = "Unread alert 1",
            IsRead = false,
            DetectedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        repo.AddAlert(new AlertLog
        {
            AlertType = "FirmChange",
            Severity = "Low",
            EntityType = "Firm",
            EntityCrd = "FIRM002",
            Summary = "Unread alert 2",
            IsRead = false,
            DetectedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        int countBefore = repo.GetUnreadCount();
        Assert.True(countBefore >= 2);

        var alerts = repo.GetRecentAlerts(10);
        repo.MarkRead(alerts[0].Id);

        int countAfter = repo.GetUnreadCount();
        Assert.Equal(countBefore - 1, countAfter);
    }

    [Fact]
    public void FirmAumAlertRuleDialog_Constructor_DoesNotThrow()
    {
        var repo = CreateRepo();
        var dlg = new FirmAumAlertRuleDialog(repo, null, null);
        Assert.NotNull(dlg);
        dlg.Dispose();
    }

    [Fact]
    public void MarketWatchRuleDialog_Constructor_DoesNotThrow()
    {
        var repo = CreateRepo();
        var dlg = new MarketWatchRuleDialog(repo, null);
        Assert.NotNull(dlg);
        dlg.Dispose();
    }
}
