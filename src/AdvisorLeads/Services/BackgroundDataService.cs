using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Manages background data population: initial bulk fetch on first run from SEC compilation
/// files and FINRA API, and periodic refresh cycles that pull fresh data and persist it to
/// the local database. This is completely separate from the UI search/filter which queries
/// the local DB only.
/// </summary>
public class BackgroundDataService
{
    private readonly FinraService _finra;
    private readonly SecCompilationService _sec;
    private readonly AdvisorRepository _repo;
    private CancellationTokenSource? _cts;
    private Task? _refreshTask;

    /// <summary>Raised on the UI thread whenever new data has been persisted.</summary>
    public event Action? DataUpdated;

    public BackgroundDataService(FinraService finra, SecCompilationService sec, AdvisorRepository repo)
    {
        _finra = finra;
        _sec = sec;
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
    /// Performs the initial bulk data fetch from SEC compilation files and FINRA API,
    /// then persists results to the database. This is intended to be called once on
    /// first run, with progress displayed to the user.
    /// </summary>
    public async Task<int> PopulateInitialDataAsync(IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report("Starting initial data population from SEC and FINRA sources...");

        int totalSaved = 0;

        // Step 1: Download and parse SEC compilation data
        try
        {
            progress?.Report("Step 1 of 3: Downloading SEC investment advisor firms...");
            var firms = await _sec.DownloadAndParseFirmsAsync(progress, cancellationToken);

            progress?.Report($"Saving {firms.Count} firms to database...");
            int firmsSaved = 0;
            foreach (var firm in firms)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    _repo.UpsertFirm(firm);
                    firmsSaved++;
                }
                catch { /* skip individual save errors */ }
            }
            progress?.Report($"✓ Saved {firmsSaved} firms from SEC data.");

            progress?.Report("Step 2 of 3: Downloading SEC investment advisor representatives...");
            var secAdvisors = await _sec.DownloadAndParseIndividualsAsync(progress, cancellationToken);

            progress?.Report($"Saving {secAdvisors.Count} SEC advisors to database...");
            int secSaved = 0;
            foreach (var advisor in secAdvisors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    _repo.UpsertAdvisor(advisor);
                    secSaved++;
                }
                catch { /* skip individual save errors */ }
            }
            totalSaved += secSaved;
            progress?.Report($"✓ Saved {secSaved} advisors from SEC data.");
        }
        catch (Exception ex)
        {
            progress?.Report($"⚠ SEC data fetch encountered errors: {ex.Message}");
            progress?.Report("Continuing with FINRA data...");
        }

        // Step 2: Fetch supplemental data from FINRA API
        try
        {
            progress?.Report("Step 3 of 3: Fetching supplemental data from FINRA BrokerCheck...");
            var finraAdvisors = await _finra.FetchBulkAdvisorsAsync(progress, cancellationToken);

            progress?.Report($"Merging {finraAdvisors.Count} FINRA records with existing database...");
            int finraSaved = 0;
            foreach (var advisor in finraAdvisors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    _repo.UpsertAdvisor(advisor);
                    finraSaved++;
                }
                catch { /* skip individual save errors */ }
            }
            totalSaved += finraSaved;
            progress?.Report($"✓ Merged {finraSaved} advisors from FINRA data.");
        }
        catch (Exception ex)
        {
            progress?.Report($"⚠ FINRA data fetch encountered errors: {ex.Message}");
        }

        progress?.Report($"✓ Initial setup complete. {totalSaved} advisors total in database.");
        DataUpdated?.Invoke();
        return totalSaved;
    }

    /// <summary>
    /// Starts a periodic background refresh that re-fetches data from SEC and FINRA
    /// and updates local records. Runs immediately on startup, then every
    /// <paramref name="intervalMinutes"/> minutes thereafter.
    /// </summary>
    public void StartBackgroundRefresh(int intervalMinutes = 60)
    {
        StopBackgroundRefresh();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _refreshTask = Task.Run(async () =>
        {
            // Run refresh immediately on startup
            try
            {
                var progress = new Progress<string>(_ => { }); // silent
                await RefreshDataAsync(progress, token);
            }
            catch (OperationCanceledException) { return; }
            catch { /* log and continue */ }

            // Then run periodically
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
                    await RefreshDataAsync(progress, token);
                }
                catch (OperationCanceledException) { break; }
                catch { /* log and continue */ }
            }
        }, token);
    }

    /// <summary>
    /// Performs a single refresh cycle: downloads SEC data and supplements with FINRA.
    /// </summary>
    private async Task RefreshDataAsync(IProgress<string> progress, CancellationToken token)
    {
        // Refresh SEC data (this will re-download the compilation files)
        try
        {
            var firms = await _sec.DownloadAndParseFirmsAsync(progress, token);
            foreach (var firm in firms)
            {
                if (token.IsCancellationRequested) break;
                try { _repo.UpsertFirm(firm); } catch { }
            }

            var advisors = await _sec.DownloadAndParseIndividualsAsync(progress, token);
            foreach (var advisor in advisors)
            {
                if (token.IsCancellationRequested) break;
                try { _repo.UpsertAdvisor(advisor); } catch { }
            }
        }
        catch { /* continue on error */ }

        // Supplement with FINRA data
        try
        {
            var finraAdvisors = await _finra.FetchBulkAdvisorsAsync(progress, token);
            foreach (var advisor in finraAdvisors)
            {
                if (token.IsCancellationRequested) break;
                try { _repo.UpsertAdvisor(advisor); } catch { }
            }
        }
        catch { /* continue on error */ }

        DataUpdated?.Invoke();
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
