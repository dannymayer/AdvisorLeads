using AdvisorLeads.Abstractions;
using AdvisorLeads.Controls;
using AdvisorLeads.Data;
using AdvisorLeads.Models;
using AdvisorLeads.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AdvisorLeads.Forms;

public class MainForm : Form
{
    private const int MainSplitDefaultDistance = 220;
    private const int ContentSplitDefaultDistance = 420;

    // Services
    private AdvisorRepository _repo = null!;
    private FinraService _finra = null!;
    private SecIapdService _secStub = null!;
    private SecCompilationService _sec = null!;
    private SecMonthlyFirmService _secMonthly = null!;
    private BrokerProtocolService _brokerProtocolService = null!;
    private DataSyncService _sync = null!;
    private BackgroundDataService _bgData = null!;
    private WealthboxService? _wealthbox;
    private HunterService? _hunterService;
    private SecIapdEnrichmentService _iapd = null!;
    private ListRepository _listRepo = null!;
    private AumAnalyticsService _aumAnalytics = null!;
    private ChangeDetectionService _changeDetection = null!;
    private FormAdvHistoricalService _formAdvHistorical = null!;
    private EdgarSubmissionsService _edgarSubmissions = null!;
    private EdgarSearchService _edgarSearch = null!;
    private MaTargetScoringService _maScoring = null!;
    private DataCleaningService _dataCleaning = null!;
    private AlertRepository _alertRepo = null!;
    private string _dbPath = null!;
    private string _wealthboxToken = string.Empty;

    // UI components
    private FilterPanel _filterPanel = null!;
    private FirmFilterPanel _firmFilterPanel = null!;
    private Panel _advisorCardPanel = null!;
    private AdvisorDetailCard _detailCard = null!;
    private SplitContainer _mainSplit = null!;
    private SplitContainer _contentSplit = null!;
    private SplitContainer _firmContentSplit = null!;
    private TabControl _mainTabs = null!;
    private ListView _firmListView = null!;
    private FirmDetailPanel _firmDetailPanel = null!;
    private StatusStrip _statusBar = null!;
    private ToolStripStatusLabel _lblStatus = null!;
    private ToolStripStatusLabel _lblCount = null!;
    private ToolStripStatusLabel _lblSync = null!;
    private MenuStrip _menuStrip = null!;
    private DashboardPanel _dashboardPanel = null!;
    private ReportingService _reportingService = null!;
    private ReportsPanel _reportsPanel = null!;
    private AlertsPanel _alertsPanel = null!;
    private TabPage _tabAlerts = null!;
    private TabPage _tabIndividuals = null!;
    private TabPage _tabFirms = null!;
    private TabPage _tabReports = null!;
    private Label _alertsBadgeLabel = null!;
    private AnalyticsPanel? _analyticsPanel;
    private GeographicAggregationService? _geoService;
    private CompetitiveIntelligenceService? _competitiveService;
    private TeamLiftDetectionService? _teamLiftService;
    private MobilityScoreService? _mobilityScoreService;
    private DisclosureScoringService? _disclosureScoringService;

    // State
    private List<Advisor> _currentAdvisors = new();
    private Advisor? _selectedAdvisor;
    private List<Firm> _currentFirms = new();
    private Firm? _selectedFirm;
    private int _totalAdvisorCount = 0;
    private int _mainSplitSavedDistance = MainSplitDefaultDistance;
    private DateTime? _lastSyncTime;
    private string? _pendingFirmCrd;
    // Tracks CRDs for which on-demand disclosure enrichment has already been triggered
    // this session, preventing infinite retry loops when FINRA returns no disclosures.
    private readonly HashSet<string> _enrichmentTriggered =
        new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _searchCts;
    private System.Windows.Forms.Timer? _dashboardRefreshTimer;

    public MainForm(IServiceProvider serviceProvider)
    {
        ResolveServices(serviceProvider);
        BuildUI();
        LoadAdvisors();
        LoadFilterOptions();
        this.Shown += OnFormShown;
    }

    private void ResolveServices(IServiceProvider provider)
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AdvisorLeads");
        _dbPath = Path.Combine(appData, "advisorleads.db");

        _repo = provider.GetRequiredService<AdvisorRepository>();
        _alertRepo = provider.GetRequiredService<AlertRepository>();
        _finra = provider.GetRequiredService<FinraService>();
        _secStub = provider.GetRequiredService<SecIapdService>();
        _sec = provider.GetRequiredService<SecCompilationService>();
        _sync = provider.GetRequiredService<DataSyncService>();
        _bgData = provider.GetRequiredService<BackgroundDataService>();
        _bgData.DataUpdated += OnBackgroundDataUpdated;
        _bgData.AlertsGenerated += OnAlertsGenerated;
        _secMonthly = provider.GetRequiredService<SecMonthlyFirmService>();
        _brokerProtocolService = provider.GetRequiredService<BrokerProtocolService>();
        _iapd = provider.GetRequiredService<SecIapdEnrichmentService>();
        _listRepo = provider.GetRequiredService<ListRepository>();

        _aumAnalytics = provider.GetRequiredService<AumAnalyticsService>();
        _changeDetection = provider.GetRequiredService<ChangeDetectionService>();
        _formAdvHistorical = provider.GetRequiredService<FormAdvHistoricalService>();
        _edgarSubmissions = provider.GetRequiredService<EdgarSubmissionsService>();
        _edgarSearch = provider.GetRequiredService<EdgarSearchService>();
        _maScoring = provider.GetRequiredService<MaTargetScoringService>();

        _dataCleaning = provider.GetRequiredService<DataCleaningService>();
        _reportingService = provider.GetRequiredService<ReportingService>();

        _disclosureScoringService = provider.GetRequiredService<DisclosureScoringService>();
        _mobilityScoreService     = provider.GetRequiredService<MobilityScoreService>();
        _geoService               = provider.GetRequiredService<GeographicAggregationService>();
        _competitiveService       = provider.GetRequiredService<CompetitiveIntelligenceService>();
        _teamLiftService          = provider.GetRequiredService<TeamLiftDetectionService>();

        _wealthbox = provider.GetService<WealthboxService>();
        _hunterService = provider.GetService<HunterService>();

        // Load saved Wealthbox token for settings UI tracking
        _wealthboxToken = LoadSetting("WealthboxToken") ?? string.Empty;

        // Load last sync time
        var syncStr = LoadSetting("LastSyncTime");
        if (DateTime.TryParse(syncStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var syncTime))
            _lastSyncTime = syncTime;
    }

    private void BuildUI()
    {
        this.Text = "AdvisorLeads – Recruiter Tool";
        this.Size = new Size(1280, 800);
        this.MinimumSize = new Size(900, 600);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Font = new Font("Segoe UI", 9);
        this.Icon = SystemIcons.Application;

        // Menu
        BuildMenu();

        // Status bar
        _statusBar = new StatusStrip();
        _lblStatus = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _lblSync = new ToolStripStatusLabel("Sync: —") { TextAlign = ContentAlignment.MiddleRight };
        _lblCount = new ToolStripStatusLabel("0 advisors") { TextAlign = ContentAlignment.MiddleRight };
        _statusBar.Items.AddRange(new ToolStripItem[] { _lblStatus, _lblSync, new ToolStripSeparator(), _lblCount });
        this.Controls.Add(_statusBar);
        UpdateSyncLabel();

        // Main split: filter | content
        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel1
        };

        // Filter panel
        _filterPanel = new FilterPanel { Dock = DockStyle.Fill };
        _filterPanel.FiltersChanged += (_, _) => LoadAdvisors();
        _mainSplit.Panel1.Controls.Add(_filterPanel);

        // Content split for individuals: list | detail
        _contentSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };

        // Advisor card panel (individuals)
        BuildAdvisorCardPanel();
        _contentSplit.Panel1.Controls.Add(_advisorCardPanel);

        // Detail card (individuals)
        _detailCard = new AdvisorDetailCard { Dock = DockStyle.Fill };
        _detailCard.ExcludeRequested += OnExcludeRequested;
        _detailCard.RestoreRequested += OnRestoreRequested;
        _detailCard.ImportCrmRequested += OnImportCrmRequested;
        _detailCard.RefreshRequested += OnRefreshRequested;
        _detailCard.AddToListRequested += (_, advisor) => OnAddToList(advisor);
        _detailCard.FavoriteRequested += OnFavoriteRequested;
        _detailCard.FirmNavigationRequested += OnFirmNavigationRequested;
        _detailCard.FindEmailRequested += OnFindEmailRequested;
        _detailCard.WatchToggleRequested += OnWatchToggleRequested;
        _contentSplit.Panel2.Controls.Add(_detailCard);

        // Content split for firms: list | detail
        _firmContentSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };

        BuildFirmListView();
        _firmFilterPanel = new FirmFilterPanel { Dock = DockStyle.Top };
        _firmFilterPanel.FiltersChanged += (_, _) => LoadFirms();

        var firmListPanel = new Panel { Dock = DockStyle.Fill };
        firmListPanel.Controls.Add(_firmListView);
        firmListPanel.Controls.Add(_firmFilterPanel);
        _firmContentSplit.Panel1.Controls.Add(firmListPanel);

        _firmDetailPanel = new FirmDetailPanel { Dock = DockStyle.Fill };
        _firmDetailPanel.SetServices(_aumAnalytics, _changeDetection, _formAdvHistorical, _edgarSubmissions, _edgarSearch, _maScoring);
        _firmDetailPanel.SetAdvisorRepository(_repo);
        _firmDetailPanel.AdvisorNavigationRequested += OnAdvisorNavigationRequested;
        _firmDetailPanel.WatchFirmToggleRequested += OnWatchFirmToggleRequested;
        _firmContentSplit.Panel2.Controls.Add(_firmDetailPanel);

        // Tab control — Dashboard (0), Individuals (1), Firms (2)
        _mainTabs = new TabControl { Dock = DockStyle.Fill };

        _dashboardPanel = new DashboardPanel(_repo) { Dock = DockStyle.Fill };
        _dashboardPanel.RefreshDataRequested += OnFetchData;
        _dashboardPanel.DataQualityCheckRequested += OnDataQualityCheck;
        _dashboardPanel.BrowseAdvisorsRequested += (_, _) => _mainTabs.SelectedTab = _tabIndividuals;
        _dashboardPanel.BrowseFirmsRequested += (_, _) => _mainTabs.SelectedTab = _tabFirms;
        _dashboardPanel.BrowseReportsRequested += (_, _) => _mainTabs.SelectedTab = _tabReports;
        _dashboardPanel.UpdateLastSync(_lastSyncTime?.ToLocalTime());
        var tabDashboard = new TabPage("Dashboard") { Padding = new Padding(0) };
        tabDashboard.Controls.Add(_dashboardPanel);

        _tabIndividuals = new TabPage("Individuals") { Padding = new Padding(0) };
        _tabIndividuals.Controls.Add(_contentSplit);
        _tabIndividuals.Controls.Add(MakeNavBar());
        _tabFirms = new TabPage("Firms") { Padding = new Padding(0) };
        _tabFirms.Controls.Add(_firmContentSplit);
        _tabFirms.Controls.Add(MakeNavBar());
        _mainTabs.TabPages.Add(tabDashboard);
        _mainTabs.TabPages.Add(_tabIndividuals);
        _mainTabs.TabPages.Add(_tabFirms);
        _reportsPanel = new ReportsPanel(_reportingService) { Dock = DockStyle.Fill };
        _tabReports = new TabPage("Reports") { Padding = new Padding(0) };
        _tabReports.Controls.Add(_reportsPanel);
        _tabReports.Controls.Add(MakeNavBar());
        _mainTabs.TabPages.Add(_tabReports);

        // Alerts tab (index 5)
        _alertsPanel = new AlertsPanel(
            _alertRepo, LoadSetting, SaveSetting,
            crd => _repo.GetFirmByCrd(crd)?.Name)
        { Dock = DockStyle.Fill };
        _alertsPanel.OpenAdvisorRequested += (_, alert) =>
        {
            _filterPanel.SetCrdOverride(alert.EntityCrd);
            _mainTabs.SelectedTab = _tabIndividuals;
        };
        _alertsPanel.OpenFirmRequested += (_, alert) =>
        {
            _pendingFirmCrd = alert.EntityCrd;
            _firmFilterPanel.Clear();
            _mainTabs.SelectedTab = _tabFirms;
        };
        _tabAlerts = new TabPage("Alerts") { Padding = new Padding(0) };
        _tabAlerts.Controls.Add(_alertsPanel);
        _tabAlerts.Controls.Add(MakeNavBar());

        // Analytics tab (index 4) — inserted before Alerts
        var tabAnalytics = new TabPage("Analytics") { Padding = new Padding(0) };
        _analyticsPanel = new AnalyticsPanel(
            _repo, _geoService!, _competitiveService!, _teamLiftService!, _mobilityScoreService!);
        _analyticsPanel.Dock = DockStyle.Fill;
        tabAnalytics.Controls.Add(_analyticsPanel);
        tabAnalytics.Controls.Add(MakeNavBar());
        _mainTabs.TabPages.Add(tabAnalytics);

        _mainTabs.TabPages.Add(_tabAlerts);

        _mainTabs.SelectedIndexChanged += (_, _) =>
        {
            try
            {
                bool shouldCollapse = _mainTabs.SelectedTab != _tabIndividuals;
                if (shouldCollapse && !_mainSplit.Panel1Collapsed)
                {
                    if (_mainSplit.SplitterDistance > 0)
                        _mainSplitSavedDistance = _mainSplit.SplitterDistance;
                    _mainSplit.Panel1Collapsed = true;
                }
                else if (!shouldCollapse && _mainSplit.Panel1Collapsed)
                {
                    _mainSplit.Panel1Collapsed = false;
                    SetSafeSplitterDistance(_mainSplit, _mainSplitSavedDistance);
                }
                var selected = _mainTabs.SelectedTab;
                if (selected == null) return;
                if (selected.Text == "Dashboard")
                {
                    _dashboardPanel.UpdateLastSync(_lastSyncTime?.ToLocalTime());
                    _ = _dashboardPanel.LoadStatsAsync();
                }
                else if (selected == _tabIndividuals)
                    LoadAdvisors();
                else if (selected == _tabFirms)
                    LoadFirms();
                else if (_mainTabs.SelectedIndex == 4)
                    _analyticsPanel?.LoadDefaultView();
                else if (selected == _tabAlerts)
                    _alertsPanel.RefreshAlerts();
            }
            catch (Exception ex)
            {
                SetStatus($"Navigation error: {ex.Message}");
            }
        };

        // Badge label overlaid on the Alerts tab header
        _alertsBadgeLabel = new Label
        {
            AutoSize = false,
            Size = new Size(20, 14),
            BackColor = Color.Red,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 7, FontStyle.Bold),
            Visible = false
        };
        _mainTabs.SizeChanged += (_, _) => PositionAlertsBadge();

        _mainSplit.Panel2.Controls.Add(_mainTabs);
        // Badge label overlays Panel2 on top of the tab headers.
        _mainSplit.Panel2.Controls.Add(_alertsBadgeLabel);
        _alertsBadgeLabel.BringToFront();
        this.Controls.Add(_mainSplit);
        this.Load += (_, _) => ApplyInitialSplitLayout();
    }

    private void ApplyInitialSplitLayout()
    {
        _mainSplit.Panel1MinSize = 220;
        _mainSplit.Panel2MinSize = 400;
        _contentSplit.Panel1MinSize = 280;
        _firmContentSplit.Panel1MinSize = 280;

        // Use proportional distances based on actual form width
        int mainDist = Math.Max(220, (int)(this.ClientSize.Width * 0.18));
        int contentDist = Math.Max(280, (int)(_mainSplit.Panel2.Width * 0.45));

        SetSafeSplitterDistance(_mainSplit, mainDist);
        SetSafeSplitterDistance(_contentSplit, contentDist);
        SetSafeSplitterDistance(_firmContentSplit, contentDist);

        // Save the distance, then collapse Panel1 — the app starts on Dashboard (tab 0).
        _mainSplitSavedDistance = _mainSplit.SplitterDistance > 0 ? _mainSplit.SplitterDistance : mainDist;
        _mainSplit.Panel1Collapsed = true;
        PositionAlertsBadge();
    }

    private static void SetSafeSplitterDistance(SplitContainer splitContainer, int preferredDistance)
    {
        var maxDistance = splitContainer.Orientation == Orientation.Vertical
            ? splitContainer.ClientSize.Width - splitContainer.Panel2MinSize - splitContainer.SplitterWidth
            : splitContainer.ClientSize.Height - splitContainer.Panel2MinSize - splitContainer.SplitterWidth;

        var minDistance = splitContainer.Panel1MinSize;

        if (maxDistance < minDistance)
        {
            return;
        }

        splitContainer.SplitterDistance = Math.Clamp(preferredDistance, minDistance, maxDistance);
    }

    private void BuildMenu()
    {
        _menuStrip = new MenuStrip { Font = new Font("Segoe UI", 9) };

        // Data menu
        var dataMenu = new ToolStripMenuItem("&Data");
        var fetchItem = new ToolStripMenuItem("&Fetch New Data...", null, OnFetchData) { ShortcutKeys = Keys.Control | Keys.F };
        var fetchEdgarItem = new ToolStripMenuItem("Fetch &EDGAR Filings", null, OnFetchEdgarFilings);
        var fetchEdgarSearchItem = new ToolStripMenuItem("Run EDGAR M&&A Search", null, OnRunEdgarSearch);
        var refreshItem = new ToolStripMenuItem("&Refresh Selected", null, (_, _) =>
        {
            if (_selectedAdvisor != null) OnRefreshRequested(this, _selectedAdvisor);
        })
        { ShortcutKeys = Keys.Control | Keys.R };
        var separatorItem = new ToolStripSeparator();
        var exitItem = new ToolStripMenuItem("E&xit", null, (_, _) => this.Close());
        dataMenu.DropDownItems.AddRange(new ToolStripItem[] { fetchItem, fetchEdgarItem, fetchEdgarSearchItem, new ToolStripSeparator(), refreshItem, separatorItem, exitItem });

        // CRM menu
        var crmMenu = new ToolStripMenuItem("&CRM");
        var importItem = new ToolStripMenuItem("Import Selected to &Wealthbox", null, (_, _) =>
        {
            if (_selectedAdvisor != null) OnImportCrmRequested(this, _selectedAdvisor);
        });
        var importAllItem = new ToolStripMenuItem("Import All Filtered to Wealthbox...", null, OnImportAllCrm);
        var settingsItem = new ToolStripMenuItem("Wealthbox &Settings...", null, OnWealthboxSettings);
        crmMenu.DropDownItems.AddRange(new ToolStripItem[] { importItem, importAllItem, new ToolStripSeparator(), settingsItem });

        // View menu
        var viewMenu = new ToolStripMenuItem("&View");
        var reloadItem = new ToolStripMenuItem("&Reload List", null, (_, _) => LoadAdvisors()) { ShortcutKeys = Keys.F5 };
        viewMenu.DropDownItems.Add(reloadItem);

        // Help menu
        var helpMenu = new ToolStripMenuItem("&Help");
        var aboutItem = new ToolStripMenuItem("&About", null, (_, _) =>
            MessageBox.Show(
                "AdvisorLeads v1.0\n\nRecruiter tool for sourcing financial advisors from FINRA BrokerCheck, SEC IAPD, and SEC EDGAR.\n\nData sources:\n• FINRA BrokerCheck: https://brokercheck.finra.org/\n• SEC IAPD: https://adviserinfo.sec.gov/\n• SEC EDGAR: https://www.sec.gov/edgar/\n\nEDGAR features:\n• Filing history tracking\n• AUM growth analytics\n• Change detection alerts\n• M&A target scoring\n• Ownership analysis",
                "About AdvisorLeads", MessageBoxButtons.OK, MessageBoxIcon.Information));
        helpMenu.DropDownItems.Add(aboutItem);

        // Debug menu
        var debugMenu = new ToolStripMenuItem("&Debug")
        {
            ForeColor = Color.FromArgb(180, 60, 60)
        };
        var resetItem = new ToolStripMenuItem("Clear All Data && Re-run Setup...", null, OnDebugReset)
        {
            ToolTipText = "Deletes all advisor data and the SEC cache, then re-runs the initial setup."
        };
        debugMenu.DropDownItems.Add(resetItem);

        // Lists menu
        var listsMenu = new ToolStripMenuItem("&Lists");
        var manageLists = new ToolStripMenuItem("&Manage Lists...", null, OnManageLists) { ShortcutKeys = Keys.Control | Keys.L };
        var addToListItem = new ToolStripMenuItem("Add Selected to List...", null, (_, _) => { if (_selectedAdvisor != null) OnAddToList(_selectedAdvisor); });
        listsMenu.DropDownItems.AddRange(new ToolStripItem[] { manageLists, new ToolStripSeparator(), addToListItem });

        _menuStrip.Items.AddRange(new ToolStripItem[] { dataMenu, crmMenu, listsMenu, viewMenu, helpMenu, debugMenu });

        // Dashboard shortcut — always the first item so it's easy to find
        var menuHome = new ToolStripMenuItem("🏠 Dashboard");
        menuHome.Click += (_, _) => _mainTabs.SelectedIndex = 0;
        _menuStrip.Items.Insert(0, menuHome);

        var menuReports = new ToolStripMenuItem("📊 &Reports");
        menuReports.Click += (_, _) => _mainTabs.SelectedIndex = 3;
        _menuStrip.Items.Insert(1, menuReports);

        // Tools menu — insert before Help
        var toolsMenu = new ToolStripMenuItem("&Tools");
        var dataQualityItem = new ToolStripMenuItem("&Data Quality Manager...", null, OnOpenDataQuality)
        {
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.Q,
            ToolTipText = "Scan for and fix data quality issues: duplicates, normalization, orphaned records"
        };
        toolsMenu.DropDownItems.Add(dataQualityItem);
        toolsMenu.DropDownItems.Add(new ToolStripSeparator());
        toolsMenu.DropDownItems.Add(new ToolStripMenuItem("Hunter.io &Settings...", null, OnHunterSettings));
        toolsMenu.DropDownItems.Add(new ToolStripMenuItem("&Find Emails (Hunter.io)...", null, OnHunterBatchEnrich));
        toolsMenu.DropDownItems.Add(new ToolStripSeparator());
        var exportAdvisorsItem = new ToolStripMenuItem("&Export Advisors...", null, (_, _) => OnExportAdvisors())
        {
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.E
        };
        var exportFirmsItem = new ToolStripMenuItem("Export &Firms...", null, (_, _) => OnExportFirms());
        toolsMenu.DropDownItems.Add(exportAdvisorsItem);
        toolsMenu.DropDownItems.Add(exportFirmsItem);
        _menuStrip.Items.Insert(_menuStrip.Items.IndexOf(helpMenu), toolsMenu);

        this.Controls.Add(_menuStrip);
        this.MainMenuStrip = _menuStrip;
    }

    /// <summary>Creates the "← Dashboard" breadcrumb bar shown at the top of every non-Dashboard tab.</summary>
    private Panel MakeNavBar()
    {
        var navBar = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = SystemColors.ControlLight };
        var lnkHome = new LinkLabel
        {
            Text = "← Dashboard",
            Location = new Point(8, 6),
            AutoSize = true,
            LinkColor = Color.FromArgb(0, 102, 204)
        };
        lnkHome.LinkClicked += (_, _) => _mainTabs.SelectedIndex = 0;
        navBar.Controls.Add(lnkHome);
        return navBar;
    }

    private void BuildAdvisorCardPanel()
    {
        _advisorCardPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BorderStyle = BorderStyle.None
        };

        var contextMenu = BuildCardContextMenu();
        contextMenu.Opening += (_, _) =>
        {
            // Walk up to find the AdvisorCard that owns the right-clicked control
            Control? src = contextMenu.SourceControl;
            while (src != null && src is not AdvisorCard)
                src = src.Parent;

            if (src is AdvisorCard clickedCard && clickedCard.Advisor != null)
            {
                foreach (Control c in _advisorCardPanel.Controls)
                    if (c is AdvisorCard ac) ac.SetSelected(false);

                clickedCard.SetSelected(true);
                _selectedAdvisor = clickedCard.Advisor;
            }
        };

        _advisorCardPanel.ContextMenuStrip = contextMenu;
        _advisorCardPanel.Resize += (_, _) => ResizeAdvisorCards();
    }

    private ContextMenuStrip BuildCardContextMenu()
    {
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("View Details", null, (_, _) => { if (_selectedAdvisor != null) ShowAdvisorDetail(_selectedAdvisor.Id); });
        contextMenu.Items.Add("Refresh Data", null, (_, _) => { if (_selectedAdvisor != null) OnRefreshRequested(this, _selectedAdvisor); });
        contextMenu.Items.Add("Add to List...", null, (_, _) => { if (_selectedAdvisor != null) OnAddToList(_selectedAdvisor); });
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("☆ Toggle Favorite", null, (_, _) => { if (_selectedAdvisor != null) OnFavoriteRequested(this, _selectedAdvisor); });
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Import to Wealthbox", null, (_, _) => { if (_selectedAdvisor != null) OnImportCrmRequested(this, _selectedAdvisor); });
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Export to Excel/CSV...", null, (_, _) => OnExportAdvisors());
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exclude from Results", null, (_, _) => { if (_selectedAdvisor != null) OnExcludeRequested(this, _selectedAdvisor); });
        contextMenu.Items.Add("Restore to Results", null, (_, _) => { if (_selectedAdvisor != null) OnRestoreRequested(this, _selectedAdvisor); });
        return contextMenu;
    }

    private void RenderAdvisorCards()
    {
        _advisorCardPanel.SuspendLayout();

        var oldControls = _advisorCardPanel.Controls.Cast<Control>().ToList();
        _advisorCardPanel.Controls.Clear();
        foreach (var c in oldControls)
            c.Dispose();

        var contextMenu = _advisorCardPanel.ContextMenuStrip!;
        int cardHeight = 110;
        int y = 6;
        int cardWidth = Math.Max(50, _advisorCardPanel.ClientSize.Width - 12);

        foreach (var advisor in _currentAdvisors)
        {
            var card = new AdvisorCard
            {
                Width = cardWidth,
                Height = cardHeight,
                Location = new Point(6, y),
                ContextMenuStrip = contextMenu
            };

            // Propagate the context menu to child controls so right-click on labels works
            foreach (Control child in card.Controls)
                child.ContextMenuStrip = contextMenu;

            card.SetAdvisor(advisor);

            card.CardClicked += (_, _) =>
            {
                foreach (Control c in _advisorCardPanel.Controls)
                    if (c is AdvisorCard ac) ac.SetSelected(false);

                card.SetSelected(true);
                _selectedAdvisor = card.Advisor;
                ShowAdvisorDetail(card.Advisor!.Id);
            };

            _advisorCardPanel.Controls.Add(card);
            y += cardHeight + 6;
        }

        _advisorCardPanel.ResumeLayout(true);
    }

    private void ResizeAdvisorCards()
    {
        int cardWidth = Math.Max(50, _advisorCardPanel.ClientSize.Width - 12);

        foreach (Control c in _advisorCardPanel.Controls)
            if (c is AdvisorCard card)
                card.Width = cardWidth;
    }

    private void BuildFirmListView()
    {
        _firmListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            Font = new Font("Segoe UI", 9),
            BorderStyle = BorderStyle.None
        };

        _firmListView.Columns.Add("Name", -2);
        _firmListView.Columns.Add("CRD", 70);
        _firmListView.Columns.Add("SEC #", 90);
        _firmListView.Columns.Add("State", 50);
        _firmListView.Columns.Add("City", 110);
        _firmListView.Columns.Add("Phone", 110);
        _firmListView.Columns.Add("Org Type", 110);
        _firmListView.Columns.Add("Employees", 80);
        _firmListView.Columns.Add("Status", 90);
        _firmListView.Columns.Add("Source", 55);
        _firmListView.Columns.Add("Updated", 90);
        _firmListView.Columns.Add("BP", 35);

        _firmListView.SelectedIndexChanged += OnFirmListViewSelectionChanged;

        var firmCtx = new ContextMenuStrip();
        firmCtx.Items.Add("Export to Excel/CSV...", null, (_, _) => OnExportFirms());
        _firmListView.ContextMenuStrip = firmCtx;
    }

    private async void LoadFirms()
    {
        try
        {
            var filter = _firmFilterPanel?.GetFilter();
            List<Firm> firms = null!;
            await Task.Run(() => firms = _repo.GetFirms(filter));
            _currentFirms = firms;

            _firmListView.BeginUpdate();
            _firmListView.Items.Clear();

            foreach (var firm in _currentFirms)
            {
                var item = new ListViewItem(firm.Name);
                item.SubItems.Add(firm.CrdNumber ?? "");
                item.SubItems.Add(firm.SECNumber ?? "");
                item.SubItems.Add(firm.State ?? "");
                item.SubItems.Add(firm.City ?? "");
                item.SubItems.Add(firm.Phone ?? "");
                item.SubItems.Add(firm.BusinessType ?? "");
                item.SubItems.Add(firm.NumberOfAdvisors?.ToString() ?? "");
                item.SubItems.Add(firm.RegistrationStatus ?? "");
                item.SubItems.Add(firm.Source ?? "");
                item.SubItems.Add(firm.UpdatedAt.ToString("yyyy-MM-dd"));
                item.SubItems.Add(firm.BrokerProtocolMember ? "✓" : "");
                item.Tag = firm.Id;
                _firmListView.Items.Add(item);
            }

            _firmListView.EndUpdate();
            _lblCount.Text = $"{_currentFirms.Count} firm{(_currentFirms.Count != 1 ? "s" : "")}";
            _lblStatus.Text = "Ready";

            // Select pending firm if navigated from advisor detail
            if (_pendingFirmCrd != null)
            {
                var pendingIdx = _currentFirms.FindIndex(f => f.CrdNumber == _pendingFirmCrd);
                _pendingFirmCrd = null;
                if (pendingIdx >= 0 && pendingIdx < _firmListView.Items.Count)
                {
                    _firmListView.Items[pendingIdx].Selected = true;
                    _firmListView.Items[pendingIdx].EnsureVisible();
                }
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Error loading firms: {ex.Message}";
        }
    }

    private void OnFirmListViewSelectionChanged(object? sender, EventArgs e)
    {
        if (_firmListView.SelectedItems.Count == 0) return;
        if (_firmListView.SelectedItems[0].Tag is not int id) return;
        _selectedFirm = _currentFirms.FirstOrDefault(f => f.Id == id);
        if (_selectedFirm != null)
            _firmDetailPanel.ShowFirm(_selectedFirm);
    }

    private void LoadAdvisors() => LoadAdvisorsAsync();

    private async void LoadAdvisorsAsync()
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        SetStatus("Loading...");
        _filterPanel.Enabled = false;
        try
        {
            var filter = _filterPanel.GetFilter();
            filter.PageNumber = 1;
            filter.PageSize = 5000;

            List<Advisor> advisors = null!;
            int total = 0;
            await Task.Run(() =>
            {
                cts.Token.ThrowIfCancellationRequested();
                var result = _repo.GetAdvisorsWithCount(filter);
                advisors = result.Advisors;
                total = result.TotalCount;
            }, cts.Token);

            if (cts.Token.IsCancellationRequested) return;

            _currentAdvisors = advisors;
            _totalAdvisorCount = total;

            RenderAdvisorCards();
            UpdateAdvisorCountLabel(filter);
            SetStatus("Ready");
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer search — ignore
        }
        catch (Exception ex)
        {
            if (!cts.Token.IsCancellationRequested)
                SetStatus($"Error loading advisors: {ex.Message}");
        }
        finally
        {
            _filterPanel.Enabled = true;
        }
    }

    private void UpdateAdvisorCountLabel(SearchFilter filter)
    {
        int showing = _currentAdvisors.Count;
        int total = _totalAdvisorCount;

        _lblCount.Text = showing == total
            ? $"{total} advisor{(total != 1 ? "s" : "")}"
            : $"{showing} of {total:N0} advisors (showing first {showing})";
    }

    private void OnFirmNavigationRequested(object? sender, string firmCrd)
    {
        _pendingFirmCrd = firmCrd;
        _firmFilterPanel.Clear();
        _mainTabs.SelectedTab = _tabFirms;
        // LoadFirms() is triggered by SelectedIndexChanged; after it completes _pendingFirmCrd is applied
    }

    private void OnAdvisorNavigationRequested(object? sender, string firmCrd)
    {
        _filterPanel.SetFirmCrdOverride(firmCrd);
        _mainTabs.SelectedTab = _tabIndividuals;
        // LoadAdvisors() is triggered by SelectedIndexChanged
    }

    private void LoadFilterOptions()
    {
        try
        {
            var states = _repo.GetDistinctStates();
            _filterPanel.PopulateStates(states);

            var firmStates = _repo.GetDistinctFirmStates();
            _firmFilterPanel.PopulateStates(firmStates);
        }
        catch { /* ignore if DB not ready */ }
    }

    private void OnFormShown(object? sender, EventArgs e)
    {
        if (!_bgData.IsDatabasePopulated())
        {
            using var dlg = new InitialSetupDialog();
            dlg.WorkFactory = (progress, token) => _bgData.PopulateInitialDataAsync(progress, token);
            dlg.ShowDialog(this);

            LoadAdvisors();
            LoadFirms();
            LoadFilterOptions();
            _ = _dashboardPanel.LoadStatsAsync();
        }

        _bgData.StartBackgroundRefresh(intervalMinutes: 60);

        // Refresh dashboard stats every 5 minutes while the app is open.
        _dashboardRefreshTimer = new System.Windows.Forms.Timer { Interval = 5 * 60 * 1000 };
        _dashboardRefreshTimer.Tick += async (_, _) =>
        {
            if (_mainTabs.SelectedIndex == 0)
                await _dashboardPanel.LoadStatsAsync();
        };
        _dashboardRefreshTimer.Start();

        // Run FINRA detail enrichmentfor advisors that have the HasDisclosures flag set
        // but no actual Disclosure records in the database yet (the most critical gap).
        // Also covers advisors missing qualifications that were skipped by bulk-fetch parsing.
        _ = Task.Run(async () =>
        {
            try
            {
                await _bgData.RunFinraEnrichmentAsync(CancellationToken.None, maxToProcess: 500);
                if (InvokeRequired)
                    BeginInvoke(() => LoadAdvisors());
                else
                    LoadAdvisors();
            }
            catch { /* non-critical */ }
        });

        // Run SEC IAPD enrichment in the background for advisors missing employment/qualifications.
        _ = Task.Run(async () =>
        {
            try
            {
                var progress = new Progress<string>(_ => { }); // silent background
                await _bgData.RunIapdEnrichmentAsync(progress, maxToProcess: 500);
                if (InvokeRequired)
                    BeginInvoke(() => LoadAdvisors());
                else
                    LoadAdvisors();
            }
            catch { /* non-critical */ }
        });

        // Check monthly SEC firm data in the background without blocking UI startup.
        _ = Task.Run(async () =>
        {
            try
            {
                var progress = new Progress<string>(msg => BeginInvoke(() => SetStatus(msg)));
                await _bgData.CheckAndUpdateSecFirmsAsync(
                    LoadSetting,
                    SaveSetting,
                    progress,
                    CancellationToken.None);
                BeginInvoke(() => LoadFirms());
            }
            catch (Exception ex)
            {
                BeginInvoke(() => SetStatus($"SEC firm update failed: {ex.Message}"));
            }
        });

        _ = Task.Run(async () =>
        {
            try
            {
                await _bgData.CheckAndUpdateBrokerProtocolAsync(LoadSetting, SaveSetting);
            }
            catch { /* non-critical */ }
        });

        // Fetch EDGAR filing history for firms with SEC numbers (background)
        _ = Task.Run(async () =>
        {
            try
            {
                var progress = new Progress<string>(msg => BeginInvoke(() => SetStatus(msg)));
                await _bgData.RunEdgarFilingsFetchAsync(progress, CancellationToken.None, maxFirms: 50);
            }
            catch { /* non-critical */ }
        });

        // Run EDGAR M&A keyword search scan (background)
        _ = Task.Run(async () =>
        {
            try
            {
                var progress = new Progress<string>(msg => BeginInvoke(() => SetStatus(msg)));
                await _bgData.RunEdgarSearchScanAsync(progress, CancellationToken.None);
            }
            catch { /* non-critical */ }
        });

        // Import Form ADV historical data if due (quarterly check)
        _ = Task.Run(async () =>
        {
            try
            {
                var progress = new Progress<string>(msg => BeginInvoke(() => SetStatus(msg)));
                await _bgData.RunFormAdvHistoricalImportAsync(
                    LoadSetting, SaveSetting, progress, CancellationToken.None);
            }
            catch { /* non-critical */ }
        });

        // SEC IAPD compilation: import firm data (AUM, compensation, custody, clients)
        _ = Task.Run(async () =>
        {
            try
            {
                var progress = new Progress<string>(msg => BeginInvoke(() => SetStatus(msg)));
                await _bgData.RunSecCompilationFirmImportAsync(progress, CancellationToken.None);
                BeginInvoke(() => LoadFirms());
            }
            catch { /* non-critical */ }
        });

        // SEC IAPD compilation: import individual advisor data (IARs)
        _ = Task.Run(async () =>
        {
            try
            {
                var progress = new Progress<string>(msg => BeginInvoke(() => SetStatus(msg)));
                await _bgData.RunSecCompilationIndividualImportAsync(progress, CancellationToken.None);
                BeginInvoke(() => LoadAdvisors());
            }
            catch { /* non-critical */ }
        });
    }

    private void OnBackgroundDataUpdated()
    {
        if (InvokeRequired)
        {
            BeginInvoke(OnBackgroundDataUpdated);
            return;
        }
        if (_mainTabs.SelectedIndex == 2)
            LoadFirms();
        else if (_mainTabs.SelectedIndex == 1)
            LoadAdvisors();
        LoadFilterOptions();

        _lastSyncTime = DateTime.UtcNow;
        SaveSetting("LastSyncTime", _lastSyncTime.Value.ToString("O"));
        UpdateSyncLabel();
        _dashboardPanel.UpdateLastSync(_lastSyncTime.Value.ToLocalTime());
        _ = _dashboardPanel.LoadStatsAsync();

        SetStatus("Background data refresh complete.");
    }

    private void ShowAdvisorDetail(int id)
    {
        var advisor = _repo.GetAdvisorById(id);
        if (advisor == null) return;
        _detailCard.ShowAdvisor(advisor);
        var firm = !string.IsNullOrEmpty(advisor.CurrentFirmCrd)
            ? _repo.GetFirmByCrd(advisor.CurrentFirmCrd)
            : null;
        _detailCard.SetFirm(firm);

        // Trigger a background detail fetch when the advisor is missing sub-collection data.
        // Bulk-imported advisors (SEC XML / FINRA bulk search) only carry summary fields;
        // employment history, registrations, qualifications, and disclosures only arrive via
        // the per-advisor detail endpoint. Fetch once per CRD per session.
        var crdNumber = advisor.CrdNumber;

        bool alreadyTriggered = false;
        if (!string.IsNullOrEmpty(crdNumber))
            lock (_enrichmentTriggered)
                alreadyTriggered = !_enrichmentTriggered.Add(crdNumber!);

        bool needsDisclosureEnrich = !string.IsNullOrEmpty(crdNumber)
            && !alreadyTriggered
            && (advisor.EmploymentHistory.Count == 0
                || advisor.Registrations.Count == 0
                || advisor.QualificationList.Count == 0
                || (advisor.HasDisclosures && advisor.Disclosures.Count == 0));

        if (needsDisclosureEnrich)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _sync.RefreshAdvisorAsync(crdNumber!, null);
                }
                catch
                {
                    // Allow retry next time this advisor is selected
                    lock (_enrichmentTriggered) { _enrichmentTriggered.Remove(crdNumber!); }
                }

                // Guard against the form being disposed while the background refresh ran.
                try
                {
                    if (!IsHandleCreated || IsDisposed) return;
                    var action = new Action(() =>
                    {
                        if (!IsHandleCreated || IsDisposed) return;
                        if (_selectedAdvisor?.Id == id) ShowAdvisorDetail(id);
                    });
                    if (InvokeRequired) BeginInvoke(action);
                    else action();
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            });
        }
    }

    // ── Event handlers ─────────────────────────────────────────────────

    private async void OnFetchData(object? sender, EventArgs e)
    {
        using var dlg = new FetchDataDialog();
        dlg.FetchRequested += async (_, args) =>
        {
            try
            {
                var progress = new Progress<string>(msg => dlg.SetProgress(msg));
                var results = await _sync.FetchAndSyncAsync(
                    args.Query, args.State,
                    args.IncludeFinra, args.IncludeSec,
                    progress);

                dlg.FetchComplete(results.NewCount, results.UpdatedCount);
                LoadAdvisors();
                LoadFilterOptions();
            }
            catch (Exception ex)
            {
                dlg.FetchFailed(ex.Message);
            }
        };
        dlg.ShowDialog(this);
    }

    private async void OnFetchEdgarFilings(object? sender, EventArgs e)
    {
        SetStatus("Fetching EDGAR filing history...");
        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            var count = await Task.Run(() =>
                _bgData.RunEdgarFilingsFetchAsync(progress, CancellationToken.None, maxFirms: 100));
            SetStatus($"EDGAR filings: {count} new filings stored.");
        }
        catch (Exception ex)
        {
            SetStatus($"EDGAR filings fetch failed: {ex.Message}");
        }
    }

    private async void OnRunEdgarSearch(object? sender, EventArgs e)
    {
        SetStatus("Running EDGAR M&A keyword search...");
        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            var count = await Task.Run(() =>
                _bgData.RunEdgarSearchScanAsync(progress, CancellationToken.None));
            SetStatus($"EDGAR search: {count} new M&A signal results found.");
        }
        catch (Exception ex)
        {
            SetStatus($"EDGAR search failed: {ex.Message}");
        }
    }

    private async void OnRefreshRequested(object? sender, Advisor advisor)
    {
        if (string.IsNullOrEmpty(advisor.CrdNumber))
        {
            SetStatus("Cannot refresh: no CRD number.");
            return;
        }

        SetStatus($"Refreshing {advisor.FullName}...");
        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            var updated = await _sync.RefreshAdvisorAsync(advisor.CrdNumber, progress);
            if (updated != null)
            {
                LoadAdvisors();
                ShowAdvisorDetail(updated.Id > 0 ? updated.Id : advisor.Id);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Refresh error: {ex.Message}");
        }
    }

    private void OnExcludeRequested(object? sender, Advisor advisor)
    {
        using var dlg = new ExclusionDialog(advisor.FullName);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _repo.SetAdvisorExcluded(advisor.Id, true, dlg.Reason);
            LoadAdvisors();
            SetStatus($"{advisor.FullName} excluded.");
        }
    }

    private void OnRestoreRequested(object? sender, Advisor advisor)
    {
        var result = MessageBox.Show(
            $"Restore {advisor.FullName} to the results list?",
            "Restore Advisor", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            _repo.SetAdvisorExcluded(advisor.Id, false);
            LoadAdvisors();
            SetStatus($"{advisor.FullName} restored.");
        }
    }

    private void OnFavoriteRequested(object? sender, Advisor advisor)
    {
        bool newState = !advisor.IsFavorited;
        _repo.SetAdvisorFavorited(advisor.Id, newState);
        LoadAdvisors();
        if (_selectedAdvisor?.Id == advisor.Id)
            ShowAdvisorDetail(advisor.Id);
        SetStatus(newState
            ? $"★ {advisor.FullName} added to favorites."
            : $"☆ {advisor.FullName} removed from favorites.");
    }

    private async void OnImportCrmRequested(object? sender, Advisor advisor)
    {
        if (_wealthbox == null || string.IsNullOrEmpty(_wealthboxToken))
        {
            var configure = MessageBox.Show(
                "Wealthbox is not configured. Configure it now?",
                "Wealthbox Not Configured",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (configure == DialogResult.Yes)
                OnWealthboxSettings(sender, EventArgs.Empty);
            return;
        }

        SetStatus($"Importing {advisor.FullName} to Wealthbox...");
        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            var crmId = await _wealthbox.ImportAdvisorAsync(advisor, progress);
            if (crmId != null)
            {
                _repo.SetAdvisorImported(advisor.Id, crmId);
                LoadAdvisors();
                if (_selectedAdvisor?.Id == advisor.Id)
                    ShowAdvisorDetail(advisor.Id);
                SetStatus($"{advisor.FullName} imported to Wealthbox (ID: {crmId}).");
            }
            else
            {
                SetStatus($"Wealthbox import failed for {advisor.FullName}.");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Wealthbox error: {ex.Message}");
        }
    }

    private async void OnImportAllCrm(object? sender, EventArgs e)
    {
        if (_wealthbox == null || string.IsNullOrEmpty(_wealthboxToken))
        {
            MessageBox.Show("Wealthbox is not configured.\nGo to CRM → Wealthbox Settings to configure.",
                "Not Configured", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var filter = _filterPanel.GetFilter();
        var toImport = _currentAdvisors.Where(a => !a.IsExcluded).ToList();

        var result = MessageBox.Show(
            $"Import {toImport.Count} advisor(s) to Wealthbox?\nThis may take a few minutes.",
            "Batch Import", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes) return;

        SetStatus("Batch importing to Wealthbox...");
        try
        {
            var progress = new Progress<string>(msg => SetStatus(msg));
            var results = await _wealthbox.ImportAdvisorsAsync(toImport, progress);

            int successCount = 0;
            foreach (var (advisorId, crmId) in results)
            {
                if (crmId != null)
                {
                    _repo.SetAdvisorImported(advisorId, crmId);
                    successCount++;
                }
            }

            LoadAdvisors();
            SetStatus($"Batch import complete: {successCount}/{toImport.Count} advisors imported.");
        }
        catch (Exception ex)
        {
            SetStatus($"Batch import error: {ex.Message}");
        }
    }

    private void OnWealthboxSettings(object? sender, EventArgs e)
    {
        using var dlg = new WealthboxSettingsDialog(_wealthboxToken);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _wealthboxToken = dlg.AccessToken;
            SaveSetting("WealthboxToken", _wealthboxToken);
            _wealthbox = string.IsNullOrEmpty(_wealthboxToken)
                ? null
                : new WealthboxService(_wealthboxToken);
            SetStatus("Wealthbox settings saved.");
        }
    }

    private void OnHunterSettings(object? sender, EventArgs e)
    {
        var currentKey = LoadSetting("HunterApiKey") ?? string.Empty;
        using var dlg = new HunterSettingsDialog(currentKey);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            var key = dlg.ApiKey;
            SaveSetting("HunterApiKey", key);
            _hunterService = string.IsNullOrEmpty(key) ? null : new HunterService(key);
            SetStatus("Hunter.io settings saved.");
        }
    }

    private async void OnHunterBatchEnrich(object? sender, EventArgs e)
    {
        if (_hunterService == null)
        {
            MessageBox.Show(
                "Hunter.io API key is not configured.\nGo to Tools > Hunter.io Settings... to add your key.",
                "Hunter.io Not Configured", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetStatus("Loading advisors for email enrichment...");

        var advisors = _repo.GetAdvisors(new SearchFilter { PageSize = 5000 });
        var toEnrich = advisors
            .Where(a => string.IsNullOrEmpty(a.Email) && !string.IsNullOrEmpty(a.CurrentFirmCrd))
            .ToList();

        if (toEnrich.Count == 0)
        {
            SetStatus("No advisors missing emails with known firm domains.");
            return;
        }

        var result = MessageBox.Show(
            $"Find emails for {toEnrich.Count} advisor(s) missing email addresses?\nThis may take a few minutes.",
            "Find Emails (Hunter.io)", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes)
        {
            SetStatus("Email enrichment cancelled.");
            return;
        }

        SetStatus($"Finding emails for {toEnrich.Count} advisors...");

        try
        {
            // Cache firm domains to avoid repeated DB lookups for the same firm
            var firmDomainCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var emailResults = new List<(int AdvisorId, string Email)>();
            int processed = 0;

            foreach (var advisor in toEnrich)
            {
                if (!firmDomainCache.TryGetValue(advisor.CurrentFirmCrd!, out var domain))
                {
                    var firm = _repo.GetFirmByCrd(advisor.CurrentFirmCrd!);
                    domain = firm?.Website is not null
                        ? HunterService.ExtractDomain(firm.Website)
                        : null;
                    firmDomainCache[advisor.CurrentFirmCrd!] = domain;
                }

                if (string.IsNullOrEmpty(domain)) continue;

                processed++;
                SetStatus($"Finding email {processed}/{toEnrich.Count}: {advisor.FullName}...");

                var found = await _hunterService.FindEmailAsync(advisor.FirstName, advisor.LastName, domain);
                if (found != null)
                    emailResults.Add((advisor.Id, found.Email));
            }

            if (emailResults.Count > 0)
                _repo.UpdateAdvisorEmails(emailResults);

            LoadAdvisors();
            SetStatus($"Email enrichment complete: {emailResults.Count} email(s) found out of {toEnrich.Count} advisors checked.");
        }
        catch (Exception ex)
        {
            SetStatus($"Email enrichment error: {ex.Message}");
        }
    }

    private async void OnFindEmailRequested(object? sender, Advisor advisor)
    {
        if (_hunterService == null)
        {
            SetStatus("Hunter.io not configured. Go to Tools > Hunter.io Settings...");
            return;
        }

        if (string.IsNullOrEmpty(advisor.CurrentFirmCrd))
        {
            SetStatus($"No firm CRD for {advisor.FullName}; cannot look up domain.");
            return;
        }

        var firm = _repo.GetFirmByCrd(advisor.CurrentFirmCrd);
        if (firm?.Website is null)
        {
            SetStatus($"No website found for {advisor.CurrentFirmName ?? advisor.CurrentFirmCrd}.");
            return;
        }

        var domain = HunterService.ExtractDomain(firm.Website);
        SetStatus($"Finding email for {advisor.FullName} at {domain}...");

        try
        {
            var result = await _hunterService.FindEmailAsync(advisor.FirstName, advisor.LastName, domain);
            if (result != null)
            {
                _repo.UpdateAdvisorEmails(new[] { (advisor.Id, result.Email) });
                SetStatus($"Found email for {advisor.FullName}: {result.Email} (confidence: {result.Score}%)");

                // Refresh the detail card if this advisor is still selected
                var refreshed = _repo.GetAdvisorById(advisor.Id);
                if (refreshed != null && _selectedAdvisor?.Id == advisor.Id)
                {
                    _selectedAdvisor = refreshed;
                    _detailCard.ShowAdvisor(refreshed);
                    var refreshedFirm = !string.IsNullOrEmpty(refreshed.CurrentFirmCrd)
                        ? _repo.GetFirmByCrd(refreshed.CurrentFirmCrd)
                        : null;
                    _detailCard.SetFirm(refreshedFirm);
                }
            }
            else
            {
                SetStatus($"No reliable email found for {advisor.FullName} at {domain}.");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Error finding email: {ex.Message}");
        }
    }

    private async void OnDebugReset(object? sender, EventArgs e)
    {
        var confirm = MessageBox.Show(
            "This will permanently delete ALL advisor and firm data from the local database " +
            "and clear the SEC cache, then re-run the initial setup from scratch.\n\n" +
            "Continue?",
            "Clear All Data & Re-run Setup",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (confirm != DialogResult.Yes) return;

        // Stop background refresh so it doesn't interfere while we wipe the DB.
        _bgData.StopBackgroundRefresh();
        var oldCards = _advisorCardPanel.Controls.Cast<Control>().ToList();
        _advisorCardPanel.Controls.Clear();
        foreach (var c in oldCards) c.Dispose();
        _currentAdvisors.Clear();
        _firmListView.Items.Clear();
        _selectedAdvisor = null;
        _selectedFirm = null;
        _currentAdvisors.Clear();
        _currentFirms.Clear();
        SetStatus("Clearing data...");

        // Wipe the database and SEC cache.
        await Task.Run(() =>
        {
            using (var clearDb = new DatabaseContext(_dbPath))
            {
                clearDb.ClearAllData();
            }
            _sec.ClearCache();
        });

        SetStatus("Data cleared. Starting setup...");
        LoadAdvisors();

        using var setupDlg = new InitialSetupDialog();
        setupDlg.WorkFactory = (progress, token) => _bgData.PopulateInitialDataAsync(progress, token);
        setupDlg.ShowDialog(this);

        // Reset filter state so the freshly populated data is not viewed through stale filters.
        _filterPanel.Clear();
        _firmFilterPanel.Clear();

        // Full UI refresh — mirror what OnBackgroundDataUpdated does.
        LoadAdvisors();
        LoadFirms();
        LoadFilterOptions();
        _lastSyncTime = DateTime.UtcNow;
        SaveSetting("LastSyncTime", _lastSyncTime.Value.ToString("O"));
        UpdateSyncLabel();
        _dashboardPanel.UpdateLastSync(_lastSyncTime.Value.ToLocalTime());
        _ = _dashboardPanel.LoadStatsAsync();
        _alertsPanel?.RefreshAlerts();

        _bgData.StartBackgroundRefresh(intervalMinutes: 60);
        SetStatus("Setup complete.");
    }

    private void OnManageLists(object? sender, EventArgs e)
    {
        using var form = new ListManagerForm(
            _listRepo, _repo, _wealthbox,
            advisorId =>
            {
                if (InvokeRequired) BeginInvoke(() => LoadAdvisors());
                else LoadAdvisors();
            });
        form.ShowDialog(this);
    }

    private void OnOpenDataQuality(object? sender, EventArgs e)
    {
        using var form = new DataQualityForm(_dataCleaning);
        form.ShowDialog(this);
    }

    private void OnAddToList(Advisor advisor)
    {
        var lists = _listRepo.GetAllLists();
        using var dlg = new AddToListDialog(lists, advisor.FullName);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        AdvisorList targetList;
        if (dlg.CreatedNewList && dlg.NewListName != null)
        {
            targetList = _listRepo.CreateList(dlg.NewListName);
        }
        else if (dlg.SelectedList != null)
        {
            targetList = dlg.SelectedList;
        }
        else return;

        bool added = _listRepo.AddToList(targetList.Id, advisor.Id);
        SetStatus(added
            ? $"{advisor.FullName} added to \"{targetList.Name}\"."
            : $"{advisor.FullName} is already in \"{targetList.Name}\".");
    }

    private void SetStatus(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetStatus(message));
            return;
        }
        _lblStatus.Text = message;
    }

    private void UpdateSyncLabel()
    {
        if (_lastSyncTime == null)
        {
            _lblSync.Text = "Sync: —";
            _lblSync.ForeColor = Color.FromArgb(120, 120, 120);
            return;
        }

        var age = DateTime.UtcNow - _lastSyncTime.Value;
        _lblSync.Text = $"Sync: {_lastSyncTime.Value.ToLocalTime():M/d h:mm tt}";
        _lblSync.ForeColor = age.TotalHours < 1
            ? Color.FromArgb(0, 140, 0)
            : age.TotalHours < 24
                ? Color.FromArgb(160, 120, 0)
                : Color.FromArgb(180, 0, 0);
    }

    private void OnDataQualityCheck(object? sender, EventArgs e)
    {
        using var dlg = new DataQualityForm(_dataCleaning);
        dlg.ShowDialog(this);
    }

    // ── Settings persistence ───────────────────────────────────────────

    private static void SaveSetting(string key, string value) => AppSettings.Save(key, value);

    private static string? LoadSetting(string key) => AppSettings.Load(key);

    private void OnExportAdvisors()
    {
        var advisors = _currentAdvisors;
        var allKeys = AdvisorExportColumns.All.Select(c => c.Key).ToList();
        var allHeaders = AdvisorExportColumns.All.Select(c => c.Header).ToList();
        var defaultKeys = AdvisorExportColumns.GetPreset("Default").Select(c => c.Key).ToList();

        using var dlg = new ExportDialog(
            title: "Export Advisors",
            allColumnKeys: allKeys,
            allColumnHeaders: allHeaders,
            selectedKeys: defaultKeys,
            presetNames: AdvisorExportColumns.PresetNames,
            loadSetting: LoadSetting,
            saveSetting: SaveSetting,
            totalRecords: advisors.Count,
            selectedCount: _selectedAdvisor != null ? 1 : 0,
            entityType: "Advisor");

        // Wire preset selector to return live column lists for built-in presets
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var selectedColumns = AdvisorExportColumns.All
            .Where(c => dlg.SelectedKeys.Contains(c.Key))
            .OrderBy(c => dlg.SelectedKeys.IndexOf(c.Key))
            .ToList();

        var records = dlg.ExportAllRecords || _selectedAdvisor == null
            ? advisors
            : new List<Advisor> { _selectedAdvisor };

        try
        {
            if (dlg.OutputFormat == "Excel")
            {
                ExportService.ExportToExcel(
                    records, selectedColumns, dlg.ChosenFilePath,
                    sheetName: "Advisors",
                    applyConditionalFormatting: dlg.ApplyConditionalFormatting,
                    rowStyleSelector: ExportService.GetAdvisorRowStyle);
            }
            else
            {
                ExportService.ExportToCsv(records, selectedColumns, dlg.ChosenFilePath);
            }
            SetStatus($"Exported {records.Count} advisor(s) to {Path.GetFileName(dlg.ChosenFilePath)}");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.ChosenFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnExportFirms()
    {
        var firms = _currentFirms;
        var allKeys = FirmExportColumns.All.Select(c => c.Key).ToList();
        var allHeaders = FirmExportColumns.All.Select(c => c.Header).ToList();
        var defaultKeys = FirmExportColumns.GetPreset("Default").Select(c => c.Key).ToList();

        using var dlg = new ExportDialog(
            title: "Export Firms",
            allColumnKeys: allKeys,
            allColumnHeaders: allHeaders,
            selectedKeys: defaultKeys,
            presetNames: FirmExportColumns.PresetNames,
            loadSetting: LoadSetting,
            saveSetting: SaveSetting,
            totalRecords: firms.Count,
            selectedCount: _firmListView.SelectedItems.Count,
            entityType: "Firm");

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var selectedColumns = FirmExportColumns.All
            .Where(c => dlg.SelectedKeys.Contains(c.Key))
            .OrderBy(c => dlg.SelectedKeys.IndexOf(c.Key))
            .ToList();

        var records = dlg.ExportAllRecords
            ? firms
            : _firmListView.SelectedItems.Cast<ListViewItem>()
                .Where(i => i.Tag is int id)
                .Select(i => firms.FirstOrDefault(f => f.Id == (int)i.Tag!))
                .Where(f => f != null).Cast<Firm>()
                .ToList();

        try
        {
            if (dlg.OutputFormat == "Excel")
            {
                ExportService.ExportToExcel(
                    records, selectedColumns, dlg.ChosenFilePath,
                    sheetName: "Firms",
                    applyConditionalFormatting: dlg.ApplyConditionalFormatting,
                    rowStyleSelector: ExportService.GetFirmRowStyle);
            }
            else
            {
                ExportService.ExportToCsv(records, selectedColumns, dlg.ChosenFilePath);
            }
            SetStatus($"Exported {records.Count} firm(s) to {Path.GetFileName(dlg.ChosenFilePath)}");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.ChosenFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _dashboardRefreshTimer?.Stop();
        _dashboardRefreshTimer?.Dispose();
        _bgData?.StopBackgroundRefresh();
        base.OnFormClosed(e);
    }

    private void OnAlertsGenerated(int alertCount)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnAlertsGenerated(alertCount)));
            return;
        }
        _alertsPanel?.RefreshAlerts();
        UpdateAlertsBadge();
    }

    private void OnWatchToggleRequested(object? sender, Advisor advisor)
    {
        bool newState = !advisor.IsWatched;
        _repo.SetAdvisorWatched(advisor.Id, newState);
        LoadAdvisors();
        if (_selectedAdvisor?.Id == advisor.Id)
            ShowAdvisorDetail(advisor.Id);
        SetStatus(newState
            ? $"👁 {advisor.FullName} added to watch list."
            : $"{advisor.FullName} removed from watch list.");
    }

    private void OnWatchFirmToggleRequested(object? sender, Firm firm)
    {
        bool newState = !firm.IsWatched;
        _repo.SetFirmWatched(firm.Id, newState);
        SetStatus(newState
            ? $"👁 {firm.Name} added to watch list."
            : $"{firm.Name} removed from watch list.");
    }

    private void PositionAlertsBadge()
    {
        if (_alertsBadgeLabel == null || !_mainTabs.IsHandleCreated) return;
        try
        {
            int alertsIdx = _mainTabs.TabPages.IndexOf(_tabAlerts);
            if (alertsIdx >= 0)
            {
                var tabRect = _mainTabs.GetTabRect(alertsIdx);
                // Position relative to Panel2, which hosts both _mainTabs and the badge label.
                _alertsBadgeLabel.Location = new Point(
                    _mainTabs.Left + tabRect.Right - _alertsBadgeLabel.Width - 1,
                    _mainTabs.Top + tabRect.Top + 1);
            }
        }
        catch { }
        UpdateAlertsBadge();
    }

    private void UpdateAlertsBadge()
    {
        if (_alertsPanel == null || _alertsBadgeLabel == null) return;
        int count = _alertsPanel.UnreadCount;
        _alertsBadgeLabel.Visible = count > 0;
        _alertsBadgeLabel.Text = count >= 99 ? "99+" : count.ToString();
    }
}
