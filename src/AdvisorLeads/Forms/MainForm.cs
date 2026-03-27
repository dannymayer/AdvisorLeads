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
    private string _dbPath = null!;
    private string _wealthboxToken = string.Empty;

    // UI components
    private FilterPanel _filterPanel = null!;
    private FirmFilterPanel _firmFilterPanel = null!;
    private FlowLayoutPanel _cardContainer = null!;
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
    private int _totalAdvisorCount = 0;
    private int _currentPage = 1;
    // Tracks CRDs for which on-demand disclosure enrichment has already been triggered
    // this session, preventing infinite retry loops when FINRA returns no disclosures.
    private readonly HashSet<string> _enrichmentTriggered =
        new(StringComparer.OrdinalIgnoreCase);
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

        // Results card grid (individuals)
        BuildCardContainer();
        _contentSplit.Panel1.Controls.Add(_cardContainer);

        // Detail card (individuals)
        _detailCard = new AdvisorDetailCard { Dock = DockStyle.Fill };
        _detailCard.ExcludeRequested += OnExcludeRequested;
        _detailCard.RestoreRequested += OnRestoreRequested;
        _detailCard.ImportCrmRequested += OnImportCrmRequested;
        _detailCard.RefreshRequested += OnRefreshRequested;
        _detailCard.AddToListRequested += (_, advisor) => OnAddToList(advisor);
        _detailCard.FavoriteRequested += OnFavoriteRequested;
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
    }

    private void BuildCardContainer()
    {
        _cardContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = false,
            WrapContents = true,
            BackColor = Color.FromArgb(240, 242, 245),
            Padding = new Padding(6)
        };
        _cardContainer.Resize += (_, _) => OnCardContainerResized();

        // Context menu (attached to individual cards on creation)
        _cardContainer.Tag = BuildCardContextMenu();
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

    private int CalculateCardsPerPage()
    {
        int w = _cardContainer.ClientSize.Width - _cardContainer.Padding.Horizontal;
        int h = _cardContainer.ClientSize.Height - _cardContainer.Padding.Vertical;
        if (w < 100 || h < 100) return 6;

        const int cardW = 270;
        const int cardH = 135;
        const int margin = 12; // 6px margin on each side

        int cols = Math.Max(1, w / (cardW + margin));
        int rows = Math.Max(1, h / (cardH + margin));
        return cols * rows;
    }

    private void OnCardContainerResized()
    {
        int newPageSize = CalculateCardsPerPage();
        var filter = _filterPanel.GetFilter();
        if (filter.PageSize != newPageSize)
        {
            filter.PageSize = newPageSize;
            LoadAdvisors();
        }
        else
        {
            LayoutCards();
        }
    }

    private void LayoutCards()
    {
        int w = _cardContainer.ClientSize.Width - _cardContainer.Padding.Horizontal;
        if (w < 100) return;

        const int cardMinW = 270;
        const int margin = 12;
        int cols = Math.Max(1, w / (cardMinW + margin));
        int cardW = (w - margin * cols) / cols;

        foreach (Control c in _cardContainer.Controls)
        {
            if (c is AdvisorCard card)
                card.Size = new Size(cardW, 135);
        }
    }

    private void RenderCards()
    {
        _cardContainer.SuspendLayout();
        _cardContainer.Controls.Clear();

        int w = _cardContainer.ClientSize.Width - _cardContainer.Padding.Horizontal;
        const int cardMinW = 270;
        const int margin = 12;
        int cols = Math.Max(1, w / (cardMinW + margin));
        int cardW = cols > 0 ? (w - margin * cols) / cols : cardMinW;

        var ctx = _cardContainer.Tag as ContextMenuStrip;

        foreach (var advisor in _currentAdvisors)
        {
            var card = new AdvisorCard();
            card.Size = new Size(cardW, 135);
            card.SetAdvisor(advisor);
            card.ContextMenuStrip = ctx;
            card.CardClicked += OnAdvisorCardClicked;
            _cardContainer.Controls.Add(card);
        }

        _cardContainer.ResumeLayout(true);
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
            filter.PageSize = CalculateCardsPerPage();

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

            RenderCards();

            UpdateAdvisorCountLabel(filter);
            SetStatus("Ready");

            int totalPages = filter.PageSize > 0 ? (int)Math.Ceiling((double)total / filter.PageSize) : 1;
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

    private void OnAdvisorCardClicked(object? sender, EventArgs e)
    {
        if (sender is not AdvisorCard clickedCard || clickedCard.Advisor == null) return;

        // Deselect previous
        foreach (Control c in _cardContainer.Controls)
        {
            if (c is AdvisorCard card)
                card.SetSelected(false);
        }

        clickedCard.SetSelected(true);
        _selectedAdvisor = clickedCard.Advisor;
        ShowAdvisorDetail(_selectedAdvisor.Id);
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
        _cardContainer.Controls.Clear();
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
        base.OnFormClosed(e);
    }
}
