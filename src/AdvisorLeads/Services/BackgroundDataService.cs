using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Manages background data population: initial bulk fetch on first run, and periodic
/// refresh cycles that pull fresh data from FINRA and persist it to the local database.
/// This is completely separate from the UI search/filter which queries the local DB only.
/// </summary>
public class BackgroundDataService
{
    private readonly FinraService _finra;
    private readonly AdvisorRepository _repo;
    private CancellationTokenSource? _cts;
    private Task? _refreshTask;

    /// <summary>Raised on the UI thread whenever new data has been persisted.</summary>
    public event Action? DataUpdated;

    public BackgroundDataService(FinraService finra, AdvisorRepository repo)
    {
        _finra = finra;
        _repo = repo;
    }

    /// <summary>
    /// Returns true if the database has already been populated with advisor data.
    /// </summary>
    public bool IsDatabasePopulated()
    {
        var filter = new SearchFilter();
        var advisors = _repo.GetAdvisors(filter);
        return advisors.Count > 0;
    }

    /// <summary>
    /// Performs the initial bulk data fetch from FINRA and persists results to the database.
    /// This is intended to be called once on first run, with progress displayed to the user.
    /// </summary>
    public async Task<int> PopulateInitialDataAsync(IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report("Starting initial data population from FINRA BrokerCheck...");

        var advisors = await _finra.FetchBulkAdvisorsAsync(progress, cancellationToken);

        progress?.Report($"Saving {advisors.Count} advisors to local database...");

        int saved = 0;
        foreach (var advisor in advisors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _repo.UpsertAdvisor(advisor);
                saved++;
            }
            catch { /* skip individual save errors */ }
        }

        progress?.Report($"✓ Initial setup complete. {saved} advisors saved to database.");
        DataUpdated?.Invoke();
        return saved;
    }

    /// <summary>
    /// Starts a periodic background refresh that re-fetches data from FINRA
    /// and updates local records. Runs every <paramref name="intervalMinutes"/> minutes.
    /// </summary>
    public void StartBackgroundRefresh(int intervalMinutes = 60)
    {
        StopBackgroundRefresh();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _refreshTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), token);
                }
                catch (OperationCanceledException) { break; }

                try
                {
                    var progress = new Progress<string>(_ => { }); // silent
                    var advisors = await _finra.FetchBulkAdvisorsAsync(progress, token);

                    foreach (var advisor in advisors)
                    {
                        if (token.IsCancellationRequested) break;
                        try { _repo.UpsertAdvisor(advisor); } catch { }
                    }

                    DataUpdated?.Invoke();
                }
                catch (OperationCanceledException) { break; }
                catch { /* log and continue */ }
            }
        }, token);
    }

    /// <summary>
    /// Stops the periodic background refresh.
    /// </summary>
    public void StopBackgroundRefresh()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _refreshTask = null;
    }
}
