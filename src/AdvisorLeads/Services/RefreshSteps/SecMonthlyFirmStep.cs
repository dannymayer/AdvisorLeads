using AdvisorLeads.Abstractions;

namespace AdvisorLeads.Services.RefreshSteps;

internal sealed class SecMonthlyFirmStep : IRefreshStep
{
    private readonly SecMonthlyFirmService _secMonthly;
    private readonly ChangeDetectionService _changeDetection;
    private readonly AumAnalyticsService _aumAnalytics;
    private readonly IAdvisorRepository _repo;

    public SecMonthlyFirmStep(
        SecMonthlyFirmService secMonthly,
        ChangeDetectionService changeDetection,
        AumAnalyticsService aumAnalytics,
        IAdvisorRepository repo)
    {
        _secMonthly = secMonthly;
        _changeDetection = changeDetection;
        _aumAnalytics = aumAnalytics;
        _repo = repo;
    }

    public string Name => "SEC Monthly Firm";
    public int OrderIndex => 10;

    public async Task ExecuteAsync(IProgress<string>? progress, CancellationToken token)
    {
        try
        {
            var latestUrl = await _secMonthly.GetLatestRegisteredAdvisersUrlAsync(progress, token);
            if (latestUrl == null)
            {
                progress?.Report("Warning: SEC Monthly Firm step — could not find latest ZIP URL.");
                return;
            }

            var firms = await _secMonthly.DownloadAndParseFirmsAsync(latestUrl, progress, token);
            if (firms.Count == 0) return;

            var eventCount = _changeDetection.DetectChanges(firms, progress);
            if (eventCount > 0)
                progress?.Report($"✓ Detected {eventCount} significant firm changes.");

            _repo.UpsertFirmBatch(firms, progress);

            var snapshotCount = _aumAnalytics.SnapshotFirmData(firms, DateTime.Now);
            if (snapshotCount > 0)
                progress?.Report($"✓ Recorded {snapshotCount:N0} AUM snapshots.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"Warning: {Name} step failed: {ex.Message}");
        }
    }
}
