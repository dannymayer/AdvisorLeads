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
    private SecMonthlyFirmService? _secMonthly;
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

    public void SetSecMonthlyService(SecMonthlyFirmService s) => _secMonthly = s;

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
    /// Performs the initial bulk data fetch using the FINRA BrokerCheck API.
    /// Covers both Investment Adviser Representatives (ind_ia_scope) and
    /// Registered Representatives (ind_bc_scope) since FINRA/IARD CRD is the
    /// shared registration system for both SEC and FINRA registrants.
    ///
    /// A limited A–Z + firm sweep runs here to populate the DB quickly (≈5–10 min).
    /// The background refresh then continues with deeper sweeps.
    /// </summary>
    public async Task<int> PopulateInitialDataAsync(IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report("Starting initial data population from FINRA BrokerCheck (CRD)...");
        progress?.Report("This covers both Investment Adviser Representatives (SEC/IARD) and Registered Representatives.");
        progress?.Report("This typically takes 5–15 minutes. You can click Skip to continue with an empty database and data will load in the background.");

        int totalSaved = 0;
        int firmsSaved = 0;

        // Step 1: FINRA individual sweep — A–Z, 20 pages per letter × 100 records = up to 52,000 records
        try
        {
            progress?.Report("Step 1 of 2: Sweeping FINRA BrokerCheck for individuals (A–Z, active registrants)...");
            var advisors = await _finra.FetchBulkAdvisorsAsync(
                progress, cancellationToken,
                maxPagesPerLetter: 20,
                activeOnly: false);

            progress?.Report($"Saving {advisors.Count} individuals to database...");
            int saved = 0;
            foreach (var advisor in advisors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { _repo.UpsertAdvisor(advisor); saved++; } catch { }
                if (saved % 1000 == 0 && saved > 0)
                    progress?.Report($"  Saved {saved:N0} of {advisors.Count:N0} individuals...");
            }
            totalSaved += saved;
            progress?.Report($"✓ Saved {saved:N0} individual records.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"⚠ Individual sweep error: {ex.Message}");
        }

        // Step 2: FINRA firm sweep — A–Z, 15 pages per letter
        try
        {
            progress?.Report("Step 2 of 2: Sweeping FINRA BrokerCheck for firms (investment advisers and broker-dealers)...");
            var firms = await _finra.FetchBulkFirmsAsync(
                progress, cancellationToken,
                maxPagesPerLetter: 15);

            progress?.Report($"Saving {firms.Count} firms to database...");
            foreach (var firm in firms)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { _repo.UpsertFirm(firm); firmsSaved++; } catch { }
            }
            progress?.Report($"✓ Saved {firmsSaved:N0} firm records.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"⚠ Firm sweep error: {ex.Message}");
        }

        progress?.Report($"✓ Initial setup complete. {totalSaved:N0} advisors and {firmsSaved:N0} firms in database.");
        progress?.Report("Background sync will continue adding records over the next hour.");
        DataUpdated?.Invoke();
        return totalSaved;
    }

    /// <summary>
    /// Starts a periodic background refresh. Waits <paramref name="initialDelayMinutes"/> before
    /// the first run (so the app is fully loaded before the refresh begins), then repeats
    /// every <paramref name="intervalMinutes"/> minutes.
    /// The first refresh fetches active FINRA registrants; subsequent refreshes include inactive.
    /// </summary>
    public void StartBackgroundRefresh(int intervalMinutes = 60, int initialDelayMinutes = 0)
    {
        StopBackgroundRefresh();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        bool isFirstRun = true;

        _refreshTask = Task.Run(async () =>
        {
            // Short delay so the UI finishes loading before the refresh begins
            try { await Task.Delay(TimeSpan.FromSeconds(30), token); }
            catch (OperationCanceledException) { return; }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var progress = new Progress<string>(_ => { }); // silent
                    await RefreshDataAsync(progress, token, activeOnly: isFirstRun);
                    isFirstRun = false;
                }
                catch (OperationCanceledException) { break; }
                catch { /* log and continue */ }

                try { await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), token); }
                catch (OperationCanceledException) { break; }
            }
        }, token);
    }

    /// <summary>
    /// Performs a single refresh cycle. Downloads fresh SEC data and sweeps FINRA.
    /// When <paramref name="activeOnly"/> is true, only active FINRA registrants are fetched
    /// (faster for first background run). Subsequent runs fetch all including inactive.
    /// </summary>
    private async Task RefreshDataAsync(IProgress<string> progress, CancellationToken token,
        bool activeOnly = false)
    {
        // FINRA individual sweep — deeper pages on non-first runs
        try
        {
            int pages = activeOnly ? 30 : 10;
            var finraAdvisors = await _finra.FetchBulkAdvisorsAsync(progress, token,
                maxPagesPerLetter: pages, activeOnly: activeOnly);
            foreach (var advisor in finraAdvisors)
            {
                if (token.IsCancellationRequested) break;
                try { _repo.UpsertAdvisor(advisor); } catch { }
            }
        }
        catch { /* continue on error */ }

        // FINRA firm sweep
        try
        {
            var finraFirms = await _finra.FetchBulkFirmsAsync(progress, token, maxPagesPerLetter: 10);
            foreach (var firm in finraFirms)
            {
                if (token.IsCancellationRequested) break;
                try { _repo.UpsertFirm(firm); } catch { }
            }
        }
        catch { /* continue on error */ }

        // Enrich advisors with full detail (employment history, exams, registered states)
        try
        {
            int maxEnrich = activeOnly ? 500 : 200;
            var enriched = await EnrichAdvisorsWithDetailAsync(progress, token, maxToProcess: maxEnrich);
            if (enriched > 0)
                progress.Report($"✓ Enriched {enriched} advisor records with full employment/qualification detail.");
        }
        catch (OperationCanceledException) { throw; }
        catch { /* continue on error */ }

        DataUpdated?.Invoke();
    }

    /// <summary>
    /// Enriches stored advisor records by fetching full detail from /search/individual/{crd}.
    /// Processes advisors in batches, prioritizing those with no qualifications stored.
    /// </summary>
    private async Task<int> EnrichAdvisorsWithDetailAsync(IProgress<string> progress,
        CancellationToken cancellationToken, int maxToProcess = 500)
    {
        var crdsToEnrich = _repo.GetCrdsNeedingEnrichment(maxToProcess);
        if (crdsToEnrich.Count == 0) return 0;

        progress.Report($"Enriching {crdsToEnrich.Count} advisor records with full detail...");
        int enriched = 0;

        foreach (var crd in crdsToEnrich)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var detail = await _finra.GetAdvisorDetailAsync(crd);
                if (detail != null)
                {
                    _repo.UpsertAdvisor(detail);
                    enriched++;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            await Task.Delay(350, cancellationToken);
            if (enriched > 0 && enriched % 50 == 0)
                progress.Report($"  Enriched {enriched}/{crdsToEnrich.Count} records...");
        }

        return enriched;
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

    /// <summary>
    /// Checks whether the SEC monthly firm file has been loaded for the current calendar month.
    /// If not, downloads and upserts the latest "Registered Investment Advisers" CSV.
    /// Saves "SecFirmLastMonth" (format yyyy-MM) to settings to avoid redundant downloads.
    /// </summary>
    public async Task<int> CheckAndUpdateSecFirmsAsync(
        Func<string, string?> getSetting,
        Action<string, string> saveSetting,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (_secMonthly == null) return 0;

        var currentMonth = DateTime.Now.ToString("yyyy-MM");
        var lastUpdated = getSetting("SecFirmLastMonth");

        if (lastUpdated == currentMonth)
        {
            progress?.Report("SEC firm data is current for this month.");
            return 0;
        }

        progress?.Report($"SEC Monthly: Checking for new firm data (last update: {lastUpdated ?? "never"})...");

        var latestUrl = await _secMonthly.GetLatestRegisteredAdvisersUrlAsync(ct);
        if (latestUrl == null)
        {
            progress?.Report("SEC Monthly: Could not find latest file URL.");
            return 0;
        }

        progress?.Report($"SEC Monthly: Found file: {Path.GetFileName(latestUrl)}");
        var firms = await _secMonthly.DownloadAndParseFirmsAsync(latestUrl, progress, ct);

        if (firms.Count > 0)
        {
            _repo.UpsertFirmBatch(firms, progress);
            saveSetting("SecFirmLastMonth", currentMonth);
            progress?.Report($"SEC Monthly: Updated {firms.Count:N0} firm records.");
            return firms.Count;
        }

        return 0;
    }

}