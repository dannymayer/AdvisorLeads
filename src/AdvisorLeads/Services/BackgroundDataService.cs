using AdvisorLeads.Abstractions;
using AdvisorLeads.Data;
using AdvisorLeads.Models;
using AdvisorLeads.Services.RefreshSteps;

namespace AdvisorLeads.Services;

/// <summary>
/// Manages background data population: initial bulk fetch on first run from SEC compilation
/// files and FINRA API, and periodic refresh cycles that pull fresh data and persist it to
/// the local database. This is completely separate from the UI search/filter which queries
/// the local DB only.
/// </summary>
public class BackgroundDataService : IBackgroundDataService
{
    private readonly IFinraProvider _finra;
    private readonly SecCompilationService _sec;
    private readonly IAdvisorRepository _repo;
    private readonly IEnumerable<IRefreshStep> _steps;
    private readonly SecMonthlyFirmService _secMonthly;
    private readonly ChangeDetectionService _changeDetection;
    private readonly AumAnalyticsService _aumAnalytics;
    private readonly SecIapdEnrichmentService _secIapd;
    private readonly BrokerProtocolService _brokerProtocol;
    private readonly EdgarSubmissionsService _edgarSubmissions;
    private readonly EdgarSearchService _edgarSearch;
    private readonly FormAdvHistoricalService _formAdvHistorical;
    private WatchListMonitorService? _watchListMonitor;
    private Func<string, string?>? _loadSetting;
    private Action<string, string>? _saveSetting;
    private CancellationTokenSource? _cts;
    private Task? _refreshTask;

    /// <summary>Raised on the UI thread whenever new data has been persisted.</summary>
    public event Action? DataUpdated;

    /// <summary>Raised when new alerts are generated during a refresh cycle. Carries the count of new alerts.</summary>
    public event Action<int>? AlertsGenerated;

    public BackgroundDataService(
        IFinraProvider finra,
        SecCompilationService sec,
        IAdvisorRepository repo,
        IEnumerable<IRefreshStep> steps,
        SecMonthlyFirmService secMonthly,
        ChangeDetectionService changeDetection,
        AumAnalyticsService aumAnalytics,
        SecIapdEnrichmentService secIapd,
        BrokerProtocolService brokerProtocol,
        EdgarSubmissionsService edgarSubmissions,
        EdgarSearchService edgarSearch,
        FormAdvHistoricalService formAdvHistorical)
    {
        _finra = finra;
        _sec = sec;
        _repo = repo;
        _steps = steps;
        _secMonthly = secMonthly;
        _changeDetection = changeDetection;
        _aumAnalytics = aumAnalytics;
        _secIapd = secIapd;
        _brokerProtocol = brokerProtocol;
        _edgarSubmissions = edgarSubmissions;
        _edgarSearch = edgarSearch;
        _formAdvHistorical = formAdvHistorical;
    }

    public void SetWatchListMonitorService(WatchListMonitorService s) => _watchListMonitor = s;

    public void SetSettingAccessors(Func<string, string?> load, Action<string, string> save)
    {
        _loadSetting = load;
        _saveSetting = save;
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
        _repo.ResolveAdvisorFirmLinks();
        _repo.UpdateFirmAdvisorCounts();
        _repo.ClassifyRegistrationLevels();
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

        // Analytics and data enrichment steps (ordered pipeline, phase 1)
        foreach (var step in _steps.Where(s => s.OrderIndex < 200).OrderBy(s => s.OrderIndex))
        {
            await step.ExecuteAsync(progress, token);
        }

        _repo.ResolveAdvisorFirmLinks();
        _repo.UpdateFirmAdvisorCounts();
        _repo.ClassifyRegistrationLevels();

        // Post-processing enrichment steps (phase 2, after repo maintenance)
        foreach (var step in _steps.Where(s => s.OrderIndex >= 200).OrderBy(s => s.OrderIndex))
        {
            await step.ExecuteAsync(progress, token);
        }

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
                    _repo.UpsertAdvisor(detail, out _, _watchListMonitor);
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

        var latestUrl = await _secMonthly.GetLatestRegisteredAdvisersUrlAsync(progress, ct);
        if (latestUrl == null)
        {
            progress?.Report("SEC Monthly: Could not find latest file URL.");
            return 0;
        }

        progress?.Report($"SEC Monthly: Found file: {Path.GetFileName(latestUrl)}");
        var firms = await _secMonthly.DownloadAndParseFirmsAsync(latestUrl, progress, ct);

        if (firms.Count > 0)
        {
            // Detect changes before upserting (needs old values for comparison)
            if (_changeDetection != null && firms.Count > 0)
            {
                var eventCount = _changeDetection.DetectChanges(firms, progress);
                if (eventCount > 0)
                    progress?.Report($"SEC Monthly: Detected {eventCount} significant firm changes.");
            }

            _repo.UpsertFirmBatch(firms, progress);

            // Snapshot AUM data for trend tracking
            if (_aumAnalytics != null)
            {
                var snapshotCount = _aumAnalytics.SnapshotFirmData(firms, DateTime.Now);
                progress?.Report($"SEC Monthly: Recorded {snapshotCount:N0} AUM snapshots.");
            }

            saveSetting("SecFirmLastMonth", currentMonth);
            progress?.Report($"SEC Monthly: Updated {firms.Count:N0} firm records.");
            return firms.Count;
        }

        return 0;
    }

    /// <summary>
    /// Runs a batch of FINRA detail enrichment for advisors that are missing qualifications
    /// or have the HasDisclosures flag set but no Disclosure records in the database.
    /// Safe to call multiple times — GetCrdsNeedingEnrichment returns empty when caught up.
    /// </summary>
    public async Task<int> RunFinraEnrichmentAsync(
        CancellationToken ct = default,
        int maxToProcess = 500)
    {
        try
        {
            var count = await EnrichAdvisorsWithDetailAsync(
                new Progress<string>(_ => { }),
                ct,
                maxToProcess);
            if (count > 0) DataUpdated?.Invoke();
            return count;
        }
        catch (OperationCanceledException) { throw; }
        catch { return 0; }
    }

    /// <summary>
    /// Runs a batch of SEC IAPD enrichment for advisors missing qualifications/employment.
    /// Safe to call multiple times — GetCrdsNeedingIapdEnrichment returns empty when caught up.
    /// </summary>
    public async Task<int> RunIapdEnrichmentAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default,
        int maxToProcess = 200)
    {
        if (_secIapd == null) return 0;
        try
        {
            var count = await _secIapd.EnrichBatchAsync(progress, ct, maxToProcess);
            if (count > 0) DataUpdated?.Invoke();
            return count;
        }
        catch (OperationCanceledException) { throw; }
        catch { return 0; }
    }

    /// <summary>
    /// Checks whether the Broker Protocol list needs updating (weekly refresh).
    /// If so, fetches the latest member list and updates firm records.
    /// </summary>
    public async Task<int> CheckAndUpdateBrokerProtocolAsync(
        Func<string, string?> getSetting,
        Action<string, string> saveSetting,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (_brokerProtocol == null) return 0;

        const string SettingKey = "BrokerProtocolLastFetch";
        var lastFetch = getSetting(SettingKey);
        if (lastFetch != null && DateTime.TryParse(lastFetch, out var last))
        {
            if ((DateTime.UtcNow - last).TotalDays < 7) return 0; // Not due yet
        }

        progress?.Report("Checking Broker Protocol member list...");
        var names = await _brokerProtocol.FetchMemberNamesAsync(ct);

        if (names.Count < 5)
        {
            progress?.Report("⚠ Broker Protocol list returned too few results — skipping update.");
            return 0;
        }

        progress?.Report($"Updating {names.Count} Broker Protocol member firms...");
        var updated = _repo.UpdateBrokerProtocolStatus(names, DateTime.UtcNow);
        saveSetting(SettingKey, DateTime.UtcNow.ToString("O"));
        progress?.Report($"✓ Marked {updated} firms as Broker Protocol members.");
        DataUpdated?.Invoke();
        return updated;
    }

    /// <summary>
    /// Fetches EDGAR filing history for firms that have SEC numbers but no filings stored.
    /// Runs as a background task; respects EDGAR rate limits (10 req/sec).
    /// </summary>
    public async Task<int> RunEdgarFilingsFetchAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default,
        int maxFirms = 100)
    {
        if (_edgarSubmissions == null) return 0;
        try
        {
            var count = await _edgarSubmissions.FetchFilingsBatchAsync(maxFirms, progress, ct);
            if (count > 0) DataUpdated?.Invoke();
            return count;
        }
        catch (OperationCanceledException) { throw; }
        catch { return 0; }
    }

    /// <summary>
    /// Runs the EDGAR full-text M&A keyword search scan.
    /// Stores results for firms matching M&A-related terms in their filings.
    /// </summary>
    public async Task<int> RunEdgarSearchScanAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (_edgarSearch == null) return 0;
        try
        {
            var count = await _edgarSearch.RunMaSearchScanAsync(progress, ct);
            if (count > 0) DataUpdated?.Invoke();
            return count;
        }
        catch (OperationCanceledException) { throw; }
        catch { return 0; }
    }

    /// <summary>
    /// Discovers and imports historical Form ADV data from the SEC FOIA website.
    /// Only processes files that haven't been imported yet (tracked by settings).
    /// </summary>
    public async Task<int> RunFormAdvHistoricalImportAsync(
        Func<string, string?> getSetting,
        Action<string, string> saveSetting,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (_formAdvHistorical == null) return 0;

        var lastImport = getSetting("FormAdvHistoricalLastImport");
        if (lastImport != null && DateTime.TryParse(lastImport, out var last))
        {
            // Only re-import quarterly
            if ((DateTime.UtcNow - last).TotalDays < 90) return 0;
        }

        try
        {
            progress?.Report("Discovering available Form ADV historical files...");
            var urls = await _formAdvHistorical.DiscoverAvailableFilesAsync(ct);
            if (urls.Count == 0)
            {
                progress?.Report("No Form ADV historical files found.");
                return 0;
            }

            int totalFilings = 0;
            int totalOwners = 0;

            // Process only the most recent file to avoid overwhelming the DB
            var url = urls.First();
            progress?.Report($"Importing Form ADV historical data from {Path.GetFileName(new Uri(url).LocalPath)}...");
            var (filings, owners) = await _formAdvHistorical.ImportHistoricalDataAsync(url, progress, ct);
            totalFilings += filings;
            totalOwners += owners;

            saveSetting("FormAdvHistoricalLastImport", DateTime.UtcNow.ToString("O"));

            if (totalFilings > 0 || totalOwners > 0)
                DataUpdated?.Invoke();

            return totalFilings + totalOwners;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"Form ADV historical import failed: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Downloads and parses the SEC IAPD compilation files (firms XML).
    /// This is a large download (~100 MB) and provides the richest firm data
    /// (AUM, compensation, custody, client types, private funds, etc.).
    /// </summary>
    public async Task<int> RunSecCompilationFirmImportAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            var firms = await _sec.DownloadAndParseFirmsAsync(progress, ct);
            if (firms.Count > 0)
            {
                _repo.UpsertFirmBatch(firms, progress);
                DataUpdated?.Invoke();
            }
            return firms.Count;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"SEC compilation firm import failed: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Downloads and parses the SEC IAPD compilation individuals XML.
    /// Provides investment advisor representative records with qualifications,
    /// employment history, registrations, and disclosure flags.
    /// </summary>
    public async Task<int> RunSecCompilationIndividualImportAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            var advisors = await _sec.DownloadAndParseIndividualsAsync(progress, ct);
            if (advisors.Count > 0)
            {
                int saved = 0;
                foreach (var advisor in advisors)
                {
                    ct.ThrowIfCancellationRequested();
                    try { _repo.UpsertAdvisor(advisor); saved++; } catch { }
                    if (saved % 5000 == 0 && saved > 0)
                        progress?.Report($"SEC Individuals: Saved {saved:N0} of {advisors.Count:N0}...");
                }
                progress?.Report($"✓ SEC Individuals: Saved {saved:N0} advisor records.");
                DataUpdated?.Invoke();
                return saved;
            }
            return 0;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            progress?.Report($"SEC compilation individual import failed: {ex.Message}");
            return 0;
        }
    }
}