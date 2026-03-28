using AdvisorLeads.Controls;
using AdvisorLeads.Data;
using AdvisorLeads.Models;
using AdvisorLeads.Services;

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
    private SecIapdEnrichmentService _iapd = null!;
    private ListRepository _listRepo = null!;
    private AumAnalyticsService _aumAnalytics = null!;
    private ChangeDetectionService _changeDetection = null!;
    private FormAdvHistoricalService _formAdvHistorical = null!;
    private EdgarSubmissionsService _edgarSubmissions = null!;
    private EdgarSearchService _edgarSearch = null!;
    private MaTargetScoringService _maScoring = null!;
    private DataCleaningService _dataCleaning = null!;
    private string _dbPath = null!;
    private string _wealthboxToken = string.Empty;

    // UI components
    private FilterPanel _filterPanel = null!;
    private FirmFilterPanel _firmFilterPanel = null!;
    private ListView _advisorListView = null!;
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

    // State
    private List<Advisor> _currentAdvisors = new();
    private Advisor? _selectedAdvisor;
    private List<Firm> _currentFirms = new();
    private Firm? _selectedFirm;
    private int _totalAdvisorCount = 0;
    private DateTime? _lastSyncTime;
    private string? _pendingFirmCrd;
    // Tracks CRDs for which on-demand disclosure enrichment has already been triggered
    // this session, preventing infinite retry loops when FINRA returns no disclosures.
    private readonly HashSet<string> _enrichmentTriggered =
        new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _searchCts;

    public MainForm()
    {
        InitializeServices();
        BuildUI();
        LoadAdvisors();
        LoadFilterOptions();
        this.Shown += OnFormShown;
    }

    private void InitializeServices()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AdvisorLeads");
        Directory.CreateDirectory(appData);
        var dbPath = Path.Combine(appData, "advisorleads.db");
        _dbPath = dbPath;

        using (var initDb = new DatabaseContext(dbPath))
        {
            initDb.InitializeDatabase();
        }
        _repo = new AdvisorRepository(dbPath);
        _finra = new FinraService();
        _secStub = new SecIapdService();
        _sec = new SecCompilationService();
        _sync = new DataSyncService(_finra, _secStub, _repo);
        _bgData = new BackgroundDataService(_finra, _sec, _repo);
        _bgData.DataUpdated += OnBackgroundDataUpdated;
        _secMonthly = new SecMonthlyFirmService();
        _bgData.SetSecMonthlyService(_secMonthly);
        _brokerProtocolService = new BrokerProtocolService();
        _bgData.SetBrokerProtocolService(_brokerProtocolService);
        _iapd = new SecIapdEnrichmentService(_repo);
        _bgData.SetIapdService(_iapd);
        _listRepo = new ListRepository(dbPath);

        // SEC EDGAR intelligence services
        _aumAnalytics = new AumAnalyticsService(dbPath);
        _changeDetection = new ChangeDetectionService(dbPath);
        _formAdvHistorical = new FormAdvHistoricalService(dbPath);
        _edgarSubmissions = new EdgarSubmissionsService(dbPath);
        _edgarSearch = new EdgarSearchService(dbPath);
        _maScoring = new MaTargetScoringService(dbPath, _aumAnalytics, _changeDetection, _formAdvHistorical, _edgarSubmissions, _edgarSearch);

        _bgData.SetAumAnalyticsService(_aumAnalytics);
        _bgData.SetChangeDetectionService(_changeDetection);
        _bgData.SetMaScoringService(_maScoring);
        _bgData.SetEdgarSubmissionsService(_edgarSubmissions);
        _bgData.SetEdgarSearchService(_edgarSearch);
        _bgData.SetFormAdvHistoricalService(_formAdvHistorical);

        var secBulkSubmissions = new SecBulkSubmissionsService(dbPath);
        _bgData.SetSecBulkSubmissionsService(secBulkSubmissions);

        _dataCleaning = new DataCleaningService(dbPath);
        // Load saved Wealthbox token
        _wealthboxToken = LoadSetting("WealthboxToken") ?? string.Empty;
        if (!string.IsNullOrEmpty(_wealthboxToken))
            _wealthbox = new WealthboxService(_wealthboxToken);

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

        // Virtual-mode advisor list (individuals)
        BuildAdvisorListView();
        _contentSplit.Panel1.Controls.Add(_advisorListView);

        // Detail card (individuals)
        _detailCard = new AdvisorDetailCard { Dock = DockStyle.Fill };
        _detailCard.ExcludeRequested += OnExcludeRequested;
        _detailCard.RestoreRequested += OnRestoreRequested;
        _detailCard.ImportCrmRequested += OnImportCrmRequested;
        _detailCard.RefreshRequested += OnRefreshRequested;
        _detailCard.AddToListRequested += (_, advisor) => OnAddToList(advisor);
        _detailCard.FavoriteRequested += OnFavoriteRequested;
        _detailCard.FirmNavigationRequested += OnFirmNavigationRequested;
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
        _firmDetailPanel.AdvisorNavigationRequested += OnAdvisorNavigationRequested;
        _firmContentSplit.Panel2.Controls.Add(_firmDetailPanel);

        // Tab control — Dashboard (0), Individuals (1), Firms (2)
        _mainTabs = new TabControl { Dock = DockStyle.Fill };

        _dashboardPanel = new DashboardPanel(_repo) { Dock = DockStyle.Fill };
        _dashboardPanel.RefreshDataRequested += OnFetchData;
        _dashboardPanel.DataQualityCheckRequested += OnDataQualityCheck;
        _dashboardPanel.UpdateLastSync(_lastSyncTime?.ToLocalTime());
        var tabDashboard = new TabPage("Dashboard") { Padding = new Padding(0) };
        tabDashboard.Controls.Add(_dashboardPanel);

        var tabIndividuals = new TabPage("Individuals") { Padding = new Padding(0) };
        tabIndividuals.Controls.Add(_contentSplit);
        var tabFirms = new TabPage("Firms") { Padding = new Padding(0) };
        tabFirms.Controls.Add(_firmContentSplit);
        _mainTabs.TabPages.Add(tabDashboard);
        _mainTabs.TabPages.Add(tabIndividuals);
        _mainTabs.TabPages.Add(tabFirms);
        _mainTabs.SelectedIndexChanged += (_, _) =>
        {
            _mainSplit.Panel1Collapsed = _mainTabs.SelectedIndex == 0;
            if (_mainTabs.SelectedIndex == 1)
                LoadAdvisors();
            else if (_mainTabs.SelectedIndex == 2)
                LoadFirms();
        };

        _mainSplit.Panel2.Controls.Add(_mainTabs);
        this.Controls.Add(_mainSplit);
        this.Load += (_, _) => ApplyInitialSplitLayout();
    }

    private void ApplyInitialSplitLayout()
    {
        _mainSplit.Panel1MinSize = 180;
        _mainSplit.Panel2MinSize = 400;
        _contentSplit.Panel1MinSize = 280;
        _firmContentSplit.Panel1MinSize = 280;

        // Use proportional distances based on actual form width
        int mainDist = Math.Max(180, (int)(this.ClientSize.Width * 0.18));
        int contentDist = Math.Max(280, (int)(_mainSplit.Panel2.Width * 0.45));

        SetSafeSplitterDistance(_mainSplit, mainDist);
        SetSafeSplitterDistance(_contentSplit, contentDist);
        SetSafeSplitterDistance(_firmContentSplit, contentDist);
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

        // Tools menu — insert before Help
        var toolsMenu = new ToolStripMenuItem("&Tools");
        var dataQualityItem = new ToolStripMenuItem("&Data Quality Manager...", null, OnOpenDataQuality)
        {
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.Q,
            ToolTipText = "Scan for and fix data quality issues: duplicates, normalization, orphaned records"
        };
        toolsMenu.DropDownItems.Add(dataQualityItem);
        _menuStrip.Items.Insert(_menuStrip.Items.IndexOf(helpMenu), toolsMenu);

        this.Controls.Add(_menuStrip);
        this.MainMenuStrip = _menuStrip;
    }

    private void BuildAdvisorListView()
    {
        _advisorListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            VirtualMode = true,
            MultiSelect = false,
            GridLines = true,
            Font = new Font("Segoe UI", 9),
            BorderStyle = BorderStyle.None
        };

        _advisorListView.Columns.Add("Name", 180);
        _advisorListView.Columns.Add("Firm", 200);
        _advisorListView.Columns.Add("State", 50);
        _advisorListView.Columns.Add("Type", 90);
        _advisorListView.Columns.Add("Yrs Exp", 65);
        _advisorListView.Columns.Add("Disclosures", 85);
        _advisorListView.Columns.Add("Source", 55);

        _advisorListView.VirtualListSize = 0;
        _advisorListView.RetrieveVirtualItem += OnRetrieveVirtualItem;
        _advisorListView.SelectedIndexChanged += OnAdvisorListViewSelectionChanged;
        _advisorListView.ContextMenuStrip = BuildCardContextMenu();
    }

    private void OnRetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        if (e.ItemIndex < 0 || e.ItemIndex >= _currentAdvisors.Count)
        {
            e.Item = new ListViewItem();
            return;
        }

        var advisor = _currentAdvisors[e.ItemIndex];
        var item = new ListViewItem(advisor.FullName);
        item.SubItems.Add(advisor.CurrentFirmName ?? "");
        item.SubItems.Add(advisor.State ?? "");
        item.SubItems.Add(advisor.RecordType switch
        {
            "Investment Advisor Representative" => "IAR",
            "Registered Representative" => "RR",
            _ => advisor.RecordType ?? ""
        });
        item.SubItems.Add(advisor.YearsOfExperience.HasValue ? advisor.YearsOfExperience.Value.ToString() : "");
        item.SubItems.Add(advisor.HasDisclosures ? $"Yes ({advisor.DisclosureCount})" : "No");
        item.SubItems.Add(advisor.Source ?? "");

        if (advisor.IsExcluded)
            item.ForeColor = Color.FromArgb(160, 160, 160);
        else if (advisor.IsImportedToCrm)
            item.ForeColor = Color.FromArgb(100, 60, 160);
        else if (advisor.HasDisclosures)
            item.ForeColor = Color.FromArgb(180, 80, 0);
        else if (advisor.IsFavorited)
            item.BackColor = Color.FromArgb(255, 252, 220);

        e.Item = item;
    }

    private void OnAdvisorListViewSelectionChanged(object? sender, EventArgs e)
    {
        if (_advisorListView.SelectedIndices.Count == 0) return;
        int idx = _advisorListView.SelectedIndices[0];
        if (idx < 0 || idx >= _currentAdvisors.Count) return;
        _selectedAdvisor = _currentAdvisors[idx];
        ShowAdvisorDetail(_selectedAdvisor.Id);
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
        contextMenu.Items.Add("Exclude from Results", null, (_, _) => { if (_selectedAdvisor != null) OnExcludeRequested(this, _selectedAdvisor); });
        contextMenu.Items.Add("Restore to Results", null, (_, _) => { if (_selectedAdvisor != null) OnRestoreRequested(this, _selectedAdvisor); });
        return contextMenu;
    }

    private void RenderAdvisorList()
    {
        _advisorListView.VirtualListSize = 0;
        _advisorListView.VirtualListSize = _currentAdvisors.Count;
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

        _firmListView.SelectedIndexChanged += OnFirmListViewSelectionChanged;
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

            RenderAdvisorList();
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
        _mainTabs.SelectedIndex = 2;
        // LoadFirms() is triggered by SelectedIndexChanged; after it completes _pendingFirmCrd is applied
    }

    private void OnAdvisorNavigationRequested(object? sender, string firmCrd)
    {
        _filterPanel.SetFirmCrdOverride(firmCrd);
        _mainTabs.SelectedIndex = 1;
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
            LoadFilterOptions();
        }

        _bgData.StartBackgroundRefresh(intervalMinutes: 60);

        // Run FINRA detail enrichment for advisors that have the HasDisclosures flag set
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

        // If the advisor has a disclosure flag but no disclosure records in the DB yet,
        // silently fetch full FINRA detail in the background and refresh the card once done.
        // This covers the common case where the bulk fetch set HasDisclosures=true but the
        // per-advisor enrichment pass had not yet reached this record.
        // _enrichmentTriggered.Add() returns false if already added, preventing infinite retries
        // in the edge case where FINRA returns HasDisclosures=true but an empty disclosures array.
        var crdNumber = advisor.CrdNumber;
        bool needsDisclosureEnrich = advisor.HasDisclosures
            && advisor.Disclosures.Count == 0
            && !string.IsNullOrEmpty(crdNumber)
            && _enrichmentTriggered.Add(crdNumber!);

        if (needsDisclosureEnrich)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _sync.RefreshAdvisorAsync(crdNumber!, null);
                }
                catch { /* non-critical background enrichment */ }

                // Guard against the form being disposed while the background refresh ran.
                try
                {
                    if (!IsHandleCreated || IsDisposed) return;

                    if (InvokeRequired)
                        BeginInvoke(new Action(() =>
                        {
                            if (!IsHandleCreated || IsDisposed) return;
                            if (_selectedAdvisor?.Id == id) ShowAdvisorDetail(id);
                        }));
                    else if (_selectedAdvisor?.Id == id)
                        ShowAdvisorDetail(id);
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

                dlg.FetchComplete(results.Count);
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
        _advisorListView.VirtualListSize = 0;
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

        LoadAdvisors();
        LoadFilterOptions();

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

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AdvisorLeads", "settings.txt");

    private static void SaveSetting(string key, string value)
    {
        try
        {
            var lines = File.Exists(SettingsPath)
                ? File.ReadAllLines(SettingsPath).ToList()
                : new List<string>();

            var existing = lines.FindIndex(l => l.StartsWith(key + "="));
            var newLine = $"{key}={value}";
            if (existing >= 0) lines[existing] = newLine;
            else lines.Add(newLine);

            File.WriteAllLines(SettingsPath, lines);
        }
        catch { /* best effort */ }
    }

    private static string? LoadSetting(string key)
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            var line = File.ReadAllLines(SettingsPath)
                .FirstOrDefault(l => l.StartsWith(key + "="));
            return line?.Substring(key.Length + 1);
        }
        catch { return null; }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _bgData?.StopBackgroundRefresh();
        base.OnFormClosed(e);
    }
}
