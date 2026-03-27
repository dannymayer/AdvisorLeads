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
    private DatabaseContext _db = null!;
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
    private string _wealthboxToken = string.Empty;

    // UI components
    private FilterPanel _filterPanel = null!;
    private FirmFilterPanel _firmFilterPanel = null!;
    private ListView _listView = null!;
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
    private MenuStrip _menuStrip = null!;

    // State
    private List<Advisor> _currentAdvisors = new();
    private Advisor? _selectedAdvisor;
    private List<Firm> _currentFirms = new();
    private Firm? _selectedFirm;
    private ListViewItem[]? _lvCache;
    private int _lvCacheStart = 0;
    private int _totalAdvisorCount = 0;
    private int _currentPage = 1;
    private ToolStripButton _btnPrevPage = null!;
    private ToolStripButton _btnNextPage = null!;

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

        var connectionString = LoadSetting("PostgresConnectionString")
            ?? "Host=localhost;Port=5432;Database=advisorleads;Username=advisorleads;Password=advisorleads";

        _db = new DatabaseContext(connectionString);
        _db.InitializeDatabase();
        _repo = new AdvisorRepository(_db);
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
        _listRepo = new ListRepository(_db);

        // Load saved Wealthbox token
        _wealthboxToken = LoadSetting("WealthboxToken") ?? string.Empty;
        if (!string.IsNullOrEmpty(_wealthboxToken))
            _wealthbox = new WealthboxService(_wealthboxToken);
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
        _lblCount = new ToolStripStatusLabel("0 advisors") { TextAlign = ContentAlignment.MiddleRight };
        _btnPrevPage = new ToolStripButton("◀ Prev") { Enabled = false };
        _btnNextPage = new ToolStripButton("Next ▶") { Enabled = false };
        _btnPrevPage.Click += (_, _) => NavigateAdvisorPage(-1);
        _btnNextPage.Click += (_, _) => NavigateAdvisorPage(+1);
        _statusBar.Items.AddRange(new ToolStripItem[] { _lblStatus, _lblCount, new ToolStripSeparator(), _btnPrevPage, _btnNextPage });
        this.Controls.Add(_statusBar);

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

        // Results list (individuals)
        BuildListView();
        _contentSplit.Panel1.Controls.Add(_listView);

        // Detail card (individuals)
        _detailCard = new AdvisorDetailCard { Dock = DockStyle.Fill };
        _detailCard.ExcludeRequested += OnExcludeRequested;
        _detailCard.RestoreRequested += OnRestoreRequested;
        _detailCard.ImportCrmRequested += OnImportCrmRequested;
        _detailCard.RefreshRequested += OnRefreshRequested;
        _detailCard.AddToListRequested += (_, advisor) => OnAddToList(advisor);
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
        _firmContentSplit.Panel2.Controls.Add(_firmDetailPanel);

        // Tab control
        _mainTabs = new TabControl { Dock = DockStyle.Fill };
        var tabIndividuals = new TabPage("Individuals") { Padding = new Padding(0) };
        tabIndividuals.Controls.Add(_contentSplit);
        var tabFirms = new TabPage("Firms") { Padding = new Padding(0) };
        tabFirms.Controls.Add(_firmContentSplit);
        _mainTabs.TabPages.Add(tabIndividuals);
        _mainTabs.TabPages.Add(tabFirms);
        _mainTabs.SelectedIndexChanged += (_, _) =>
        {
            if (_mainTabs.SelectedIndex == 0)
                LoadAdvisors();
            else
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

        SetSafeSplitterDistance(_mainSplit, MainSplitDefaultDistance);
        SetSafeSplitterDistance(_contentSplit, ContentSplitDefaultDistance);
        SetSafeSplitterDistance(_firmContentSplit, ContentSplitDefaultDistance);
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
        var refreshItem = new ToolStripMenuItem("&Refresh Selected", null, (_, _) =>
        {
            if (_selectedAdvisor != null) OnRefreshRequested(this, _selectedAdvisor);
        })
        { ShortcutKeys = Keys.Control | Keys.R };
        var separatorItem = new ToolStripSeparator();
        var exitItem = new ToolStripMenuItem("E&xit", null, (_, _) => this.Close());
        dataMenu.DropDownItems.AddRange(new ToolStripItem[] { fetchItem, refreshItem, separatorItem, exitItem });

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
                "AdvisorLeads v1.0\n\nRecruiter tool for sourcing financial advisors from FINRA BrokerCheck and SEC IAPD.\n\nData sources:\n• FINRA BrokerCheck: https://brokercheck.finra.org/\n• SEC IAPD: https://adviserinfo.sec.gov/",
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
        this.Controls.Add(_menuStrip);
        this.MainMenuStrip = _menuStrip;
        this.Controls.Add(_menuStrip);
        this.MainMenuStrip = _menuStrip;
    }

    private void BuildListView()
    {
        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            VirtualMode = true,
            Font = new Font("Segoe UI", 9),
            BorderStyle = BorderStyle.None
        };

        _listView.Columns.Add("Name", 180);
        _listView.Columns.Add("Type", 95);
        _listView.Columns.Add("CRD", 70);
        _listView.Columns.Add("Firm", 175);
        _listView.Columns.Add("State", 50);
        _listView.Columns.Add("City", 65);
        _listView.Columns.Add("Status", 90);
        _listView.Columns.Add("Licenses", 100);
        _listView.Columns.Add("Exp.", 50);
        _listView.Columns.Add("Disclosures", 85);
        _listView.Columns.Add("Source", 70);
        _listView.Columns.Add("Updated", 90);

        _listView.RetrieveVirtualItem += OnRetrieveVirtualItem;
        _listView.CacheVirtualItems += OnCacheVirtualItems;
        _listView.SelectedIndexChanged += OnListViewSelectionChanged;
        _listView.ColumnClick += OnColumnClick;

        // Context menu
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("View Details", null, (_, _) => { if (_selectedAdvisor != null) ShowAdvisorDetail(_selectedAdvisor.Id); });
        contextMenu.Items.Add("Refresh Data", null, (_, _) => { if (_selectedAdvisor != null) OnRefreshRequested(this, _selectedAdvisor); });
        contextMenu.Items.Add("Add to List...", null, (_, _) => { if (_selectedAdvisor != null) OnAddToList(_selectedAdvisor); });
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Import to Wealthbox", null, (_, _) => { if (_selectedAdvisor != null) OnImportCrmRequested(this, _selectedAdvisor); });
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exclude from Results", null, (_, _) => { if (_selectedAdvisor != null) OnExcludeRequested(this, _selectedAdvisor); });
        contextMenu.Items.Add("Restore to Results", null, (_, _) => { if (_selectedAdvisor != null) OnRestoreRequested(this, _selectedAdvisor); });
        _listView.ContextMenuStrip = contextMenu;
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

        _firmListView.Columns.Add("Name", 200);
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

    private void LoadAdvisors() => LoadAdvisorsPageAsync(1);

    private async void LoadAdvisorsPageAsync(int page)
    {
        SetStatus("Loading...");
        _filterPanel.Enabled = false;
        _btnPrevPage.Enabled = false;
        _btnNextPage.Enabled = false;
        try
        {
            var filter = _filterPanel.GetFilter();
            filter.PageNumber = page;

            List<Advisor> advisors = null!;
            int total = 0;
            await Task.Run(() =>
            {
                advisors = _repo.GetAdvisors(filter);
                total = advisors.Count < filter.PageSize
                    ? (page - 1) * filter.PageSize + advisors.Count
                    : _repo.GetAdvisorCount(filter);
            });

            _currentAdvisors = advisors;
            _totalAdvisorCount = total;
            _currentPage = page;

            _lvCache = null;
            _lvCacheStart = 0;
            _listView.VirtualListSize = 0;
            _listView.VirtualListSize = _currentAdvisors.Count;
            _listView.Invalidate();

            UpdateAdvisorCountLabel(filter);
            SetStatus("Ready");

            int totalPages = (int)Math.Ceiling((double)total / filter.PageSize);
            _btnPrevPage.Enabled = page > 1;
            _btnNextPage.Enabled = page < totalPages;
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading advisors: {ex.Message}");
        }
        finally
        {
            _filterPanel.Enabled = true;
        }
    }

    private void NavigateAdvisorPage(int delta)
    {
        int page = Math.Max(1, _currentPage + delta);
        LoadAdvisorsPageAsync(page);
    }

    private void UpdateAdvisorCountLabel(SearchFilter filter)
    {
        int showing = _currentAdvisors.Count;
        int total = _totalAdvisorCount;
        int page = filter.PageNumber;
        int pageSize = filter.PageSize;

        if (total <= pageSize)
            _lblCount.Text = $"{total} advisor{(total != 1 ? "s" : "")}";
        else
        {
            int totalPages = (int)Math.Ceiling((double)total / pageSize);
            _lblCount.Text = $"{showing} of {total:N0} advisors (page {page}/{totalPages})";
        }
    }

    private ListViewItem BuildListViewItem(Advisor advisor)
    {
        var item = new ListViewItem(advisor.FullName);
        item.SubItems.Add(advisor.RecordType ?? "");
        item.SubItems.Add(advisor.CrdNumber ?? "");
        item.SubItems.Add(advisor.CurrentFirmName ?? "");
        item.SubItems.Add(advisor.State ?? "");
        item.SubItems.Add(advisor.City ?? "");
        item.SubItems.Add(advisor.RegistrationStatus ?? "");
        item.SubItems.Add(advisor.Licenses ?? "");
        item.SubItems.Add(advisor.YearsOfExperience.HasValue ? advisor.YearsOfExperience.Value.ToString() : "");
        item.SubItems.Add(advisor.HasDisclosures ? $"Yes ({advisor.DisclosureCount})" : "No");
        item.SubItems.Add(advisor.Source ?? "");
        item.SubItems.Add(advisor.UpdatedAt.ToString("yyyy-MM-dd"));
        item.Tag = advisor.Id;

        if (advisor.IsExcluded)
        {
            item.ForeColor = Color.Gray;
            item.Font = new Font("Segoe UI", 9, FontStyle.Strikeout);
        }
        else if (advisor.HasDisclosures)
        {
            item.BackColor = Color.FromArgb(255, 250, 240);
        }
        else if (!advisor.IsImportedToCrm
            && advisor.RegistrationStatus == "Active"
            && advisor.RecordType == "Investment Advisor Representative")
        {
            item.BackColor = Color.FromArgb(240, 255, 240);
        }
        if (advisor.IsImportedToCrm)
            item.ForeColor = Color.FromArgb(100, 60, 160);

        return item;
    }

    private void OnRetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        if (_lvCache != null
            && e.ItemIndex >= _lvCacheStart
            && e.ItemIndex < _lvCacheStart + _lvCache.Length)
        {
            e.Item = _lvCache[e.ItemIndex - _lvCacheStart];
            return;
        }
        if (e.ItemIndex >= 0 && e.ItemIndex < _currentAdvisors.Count)
            e.Item = BuildListViewItem(_currentAdvisors[e.ItemIndex]);
        else
            e.Item = new ListViewItem("?");
    }

    private void OnCacheVirtualItems(object? sender, CacheVirtualItemsEventArgs e)
    {
        if (_lvCache != null
            && e.StartIndex >= _lvCacheStart
            && e.EndIndex <= _lvCacheStart + _lvCache.Length - 1)
            return;

        _lvCacheStart = e.StartIndex;
        int len = e.EndIndex - e.StartIndex + 1;
        _lvCache = new ListViewItem[len];
        for (int i = 0; i < len; i++)
        {
            int idx = e.StartIndex + i;
            _lvCache[i] = idx < _currentAdvisors.Count
                ? BuildListViewItem(_currentAdvisors[idx])
                : new ListViewItem("?");
        }
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

        // Run SEC IAPD enrichment in the background for advisors missing employment/qualifications.
        _ = Task.Run(async () =>
        {
            try
            {
                var progress = new Progress<string>(_ => { }); // silent background
                await _bgData.RunIapdEnrichmentAsync(progress, maxToProcess: 200);
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
    }

    private void OnBackgroundDataUpdated()
    {
        if (InvokeRequired)
        {
            BeginInvoke(OnBackgroundDataUpdated);
            return;
        }
        if (_mainTabs.SelectedIndex == 1)
            LoadFirms();
        else
            LoadAdvisors();
        LoadFilterOptions();
        SetStatus("Background data refresh complete.");
    }

    private void OnListViewSelectionChanged(object? sender, EventArgs e)
    {
        if (_listView.SelectedIndices.Count == 0) return;
        int idx = _listView.SelectedIndices[0];
        if (idx < 0 || idx >= _currentAdvisors.Count) return;
        _selectedAdvisor = _currentAdvisors[idx];

        if (_selectedAdvisor != null)
            ShowAdvisorDetail(_selectedAdvisor.Id);
    }

    private void ShowAdvisorDetail(int id)
    {
        var advisor = _repo.GetAdvisorById(id);
        if (advisor != null)
            _detailCard.ShowAdvisor(advisor);
    }

    private void OnColumnClick(object? sender, ColumnClickEventArgs e)
    {
        // Simple column sorting via filter
        var filter = _filterPanel.GetFilter();
        var col = e.Column switch
        {
            0 => "LastName",
            1 => "RecordType",
            2 => "CrdNumber",
            3 => "CurrentFirmName",
            4 => "State",
            // 5 = City — no direct sort supported
            6 => "RegistrationStatus",
            // 7 = Licenses — no sort
            8 => "YearsOfExperience",
            // 9 = Disclosures
            11 => "UpdatedAt",
            _ => "LastName"
        };
        if (filter.SortBy == col)
            filter.SortDescending = !filter.SortDescending;
        else
        {
            filter.SortBy = col;
            filter.SortDescending = false;
        }
        LoadAdvisors();
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
        _lvCache = null;
        _lvCacheStart = 0;
        _listView.VirtualListSize = 0;
        _firmListView.Items.Clear();
        _selectedAdvisor = null;
        _selectedFirm = null;
        _currentAdvisors.Clear();
        _currentFirms.Clear();
        SetStatus("Clearing data...");

        // Wipe the database and SEC cache.
        await Task.Run(() =>
        {
            _db.ClearAllData();
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
        _db?.Dispose();
        base.OnFormClosed(e);
    }
}
