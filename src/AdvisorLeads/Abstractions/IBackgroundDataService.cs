namespace AdvisorLeads.Abstractions;

public interface IBackgroundDataService
{
    event Action? DataUpdated;
    event Action<int>? AlertsGenerated;
    bool IsDatabasePopulated();
    Task<int> PopulateInitialDataAsync(IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
    void StartBackgroundRefresh(int intervalMinutes = 60, int initialDelayMinutes = 0);
    void StopBackgroundRefresh();
}
