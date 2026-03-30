using AdvisorLeads.Controls;
using AdvisorLeads.Data;
using AdvisorLeads.Services;

namespace AdvisorLeads.Tests.Controls;

public class AnalyticsPanelTests : IDisposable
{
    private readonly string _dbPath;

    public AnalyticsPanelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"analytics_test_{Guid.NewGuid():N}.db");
        using var ctx = new DatabaseContext(_dbPath);
        ctx.InitializeDatabase();
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    private AdvisorRepository CreateRepo() => new AdvisorRepository(_dbPath);

    [Fact]
    public void AnalyticsPanel_Constructor_DoesNotThrow()
    {
        var repo         = CreateRepo();
        var geo          = new GeographicAggregationService(repo);
        var competitive  = new CompetitiveIntelligenceService(repo);
        var teamLift     = new TeamLiftDetectionService(_dbPath);
        var disclosure   = new DisclosureScoringService(repo);
        var mobility     = new MobilityScoreService(repo, disclosure);

        var panel = new AnalyticsPanel(repo, geo, competitive, teamLift, mobility);
        Assert.NotNull(panel);
        panel.Dispose();
    }

    [Fact]
    public void StateHeatMapPanel_Constructor_DoesNotThrow()
    {
        var panel = new StateHeatMapPanel();
        Assert.NotNull(panel);
        panel.Dispose();
    }

    [Fact]
    public void StateHeatMapPanel_LoadData_DoesNotThrow()
    {
        var panel = new StateHeatMapPanel();
        var data  = new List<StateAggregation>
        {
            new("CA", "California", 5000, 4200, 1_200_000_000m, 240_000m, 0.05, 12, 4),
            new("TX", "Texas",      3000, 2500,   800_000_000m, 266_000m, 0.04,  8, 2),
            new("NY", "New York",   4500, 3900, 1_100_000_000m, 244_000m, 0.06, 15, 5),
        };
        panel.LoadData(data);
        Assert.NotNull(panel);
        panel.Dispose();
    }

    [Fact]
    public void GeographicAggregationService_GetAdvisorCountRange_ReturnsCorrectMinMax()
    {
        var repo    = CreateRepo();
        var service = new GeographicAggregationService(repo);

        var data = new List<StateAggregation>
        {
            new("CA", "California",   5000, 4200, null, null, 0.05, 0, 0),
            new("WY", "Wyoming",        50,   40, null, null, 0.02, 0, 0),
            new("TX", "Texas",        3000, 2500, null, null, 0.04, 0, 0),
        };

        var (min, max) = service.GetAdvisorCountRange(data);

        Assert.Equal(50,   min);
        Assert.Equal(5000, max);
    }

    [Fact]
    public void GeographicAggregationService_GetAdvisorCountRange_EmptyList_ReturnsZeros()
    {
        var repo    = CreateRepo();
        var service = new GeographicAggregationService(repo);

        var (min, max) = service.GetAdvisorCountRange(new List<StateAggregation>());

        Assert.Equal(0, min);
        Assert.Equal(0, max);
    }

    [Fact]
    public void CompetitiveIntelligenceService_GetFirmGrowthShrinkData_ReturnsResults()
    {
        var repo    = CreateRepo();
        var service = new CompetitiveIntelligenceService(repo);

        // On an empty database the call should succeed and return an empty list.
        var results = service.GetFirmGrowthShrinkData(null, 50);

        Assert.NotNull(results);
    }
}
