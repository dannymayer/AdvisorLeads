using AdvisorLeads.Abstractions;
using AdvisorLeads.Data;
using AdvisorLeads.Services;
using AdvisorLeads.Services.RefreshSteps;
using Microsoft.Extensions.DependencyInjection;

namespace AdvisorLeads;

public static class ApplicationServices
{
    public static IServiceProvider Configure(string dbPath, string appDataPath)
    {
        var services = new ServiceCollection();

        using (var initDb = new DatabaseContext(dbPath))
            initDb.InitializeDatabase();

        // Core data repositories
        var repo = new AdvisorRepository(dbPath);
        var alertRepo = new AlertRepository(dbPath);
        var listRepo = new ListRepository(dbPath);

        // External data providers
        var finra = new FinraService();
        var secStub = new SecIapdService();
        var sec = new SecCompilationService();
        var secMonthly = new SecMonthlyFirmService();
        var brokerProtocol = new BrokerProtocolService();
        var iapd = new SecIapdEnrichmentService(repo);

        // SEC EDGAR intelligence services
        var aumAnalytics = new AumAnalyticsService(dbPath);
        var changeDetection = new ChangeDetectionService(dbPath);
        var formAdvHistorical = new FormAdvHistoricalService(dbPath);
        var edgarSubmissions = new EdgarSubmissionsService(dbPath);
        var edgarSearch = new EdgarSearchService(dbPath);
        var maScoring = new MaTargetScoringService(
            dbPath, aumAnalytics, changeDetection, formAdvHistorical, edgarSubmissions, edgarSearch);

        var secBulkSubmissions = new SecBulkSubmissionsService(dbPath);

        // Analytics & intelligence services
        var disclosureScoring = new DisclosureScoringService(repo);
        var mobilityScore = new MobilityScoreService(repo, disclosureScoring);
        var geoService = new GeographicAggregationService(repo);
        var competitiveService = new CompetitiveIntelligenceService(repo);
        var teamLift = new TeamLiftDetectionService(dbPath);

        // Cat1 enrichment services
        var finraSanction = new FinraSanctionService(finra, repo);
        var secEnforcement = new SecEnforcementService(repo);
        var courtListener = new CourtListenerService(repo, AppSettings.Load("CourtListenerApiToken"));
        var formAdvDeep = new FormAdvDeepEnrichmentService(repo);

        // Refresh pipeline steps
        var refreshSteps = new IRefreshStep[]
        {
            new SecMonthlyFirmStep(secMonthly, changeDetection, aumAnalytics, repo),
            new SecIapdEnrichmentStep(iapd),
            new EdgarSubmissionsStep(edgarSubmissions),
            new EdgarSearchStep(edgarSearch),
            new FormAdvHistoricalStep(formAdvHistorical, AppSettings.Load, AppSettings.Save),
            new BrokerProtocolStep(brokerProtocol, repo),
            new SecBulkSubmissionsStep(secBulkSubmissions),
            new DisclosureScoringStep(disclosureScoring),
            new MobilityScoreStep(mobilityScore),
            new CompetitiveIntelligenceStep(competitiveService),
            new FinraSanctionStep(finraSanction),
            new SecEnforcementStep(secEnforcement),
            new CourtListenerStep(courtListener),
            new FormAdvDeepEnrichmentStep(formAdvDeep),
        };

        // Sync and background services
        var sync = new DataSyncService(new IAdvisorDataSource[] { finra, secStub }, repo);
        var bgData = new BackgroundDataService(
            finra, sec, repo, refreshSteps,
            secMonthly, changeDetection, aumAnalytics,
            iapd, brokerProtocol, edgarSubmissions, edgarSearch, formAdvHistorical);
        bgData.SetSettingAccessors(AppSettings.Load, AppSettings.Save);
        bgData.SetAlertRepository(alertRepo);

        // Optional token-based services
        var wealthboxToken = AppSettings.Load("WealthboxToken") ?? string.Empty;
        WealthboxService? wealthbox = !string.IsNullOrEmpty(wealthboxToken)
            ? new WealthboxService(wealthboxToken)
            : null;

        var hunterKey = AppSettings.Load("HunterApiKey");
        HunterService? hunter = !string.IsNullOrEmpty(hunterKey)
            ? new HunterService(hunterKey!)
            : null;

        var dataCleaning = new DataCleaningService(dbPath);
        var reporting = new ReportingService(dbPath);

        // Register concrete singletons
        services.AddSingleton(repo);
        services.AddSingleton(alertRepo);
        services.AddSingleton(listRepo);
        services.AddSingleton(finra);
        services.AddSingleton(secStub);
        services.AddSingleton(sec);
        services.AddSingleton(secMonthly);
        services.AddSingleton(brokerProtocol);
        services.AddSingleton(iapd);
        services.AddSingleton(sync);
        services.AddSingleton(bgData);
        services.AddSingleton(aumAnalytics);
        services.AddSingleton(changeDetection);
        services.AddSingleton(formAdvHistorical);
        services.AddSingleton(edgarSubmissions);
        services.AddSingleton(edgarSearch);
        services.AddSingleton(maScoring);
        services.AddSingleton(secBulkSubmissions);
        services.AddSingleton(disclosureScoring);
        services.AddSingleton(mobilityScore);
        services.AddSingleton(geoService);
        services.AddSingleton(competitiveService);
        services.AddSingleton(teamLift);
        services.AddSingleton(dataCleaning);
        services.AddSingleton(reporting);

        // Register interface mappings
        services.AddSingleton<IAdvisorRepository>(repo);
        services.AddSingleton<IFinraProvider>(finra);
        services.AddSingleton<IDataSyncService>(sync);
        services.AddSingleton<IBackgroundDataService>(bgData);

        if (wealthbox != null)
        {
            services.AddSingleton(wealthbox);
            services.AddSingleton<ICrmProvider>(wealthbox);
        }

        if (hunter != null)
            services.AddSingleton(hunter);

        return services.BuildServiceProvider();
    }
}
