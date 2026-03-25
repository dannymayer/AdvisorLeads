using AdvisorLeads.Controls;
using AdvisorLeads.Data;
using AdvisorLeads.Models;
using AdvisorLeads.Services;

namespace AdvisorLeads.Forms;

public class MainForm : Form
{
    // Services
    private DatabaseContext _db = null!;
    private AdvisorRepository _repo = null!;
    private FinraService _finra = null!;
    private SecIapdService _sec = null!;
    private DataSyncService _sync = null!;
    private WealthboxService? _wealthbox;
    private string _wealthboxToken = string.Empty;

    // UI components
    private FilterPanel _filterPanel = null!;
    private ListView _listView = null!;
    private AdvisorDetailCard _detailCard = null!;
    private SplitContainer _mainSplit = null!;
    private SplitContainer _contentSplit = null!;
    private StatusStrip _statusBar = null!;
    private ToolStripStatusLabel _lblStatus = null!;
    private ToolStripStatusLabel _lblCount = null!;
    private MenuStrip _menuStrip = null!;

    // State
    private List<Advisor> _currentAdvisors = new();
    private Advisor? _selectedAdvisor;

    public MainForm()
    {
        InitializeServices();
        BuildUI();
        LoadAdvisors();
        LoadFilterOptions();
    }

    private void InitializeServices()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AdvisorLeads");
        Directory.CreateDirectory(appData);
        var dbPath = Path.Combine(appData, "advisorleads.db");

        _db = new DatabaseContext(dbPath);
        _db.InitializeDatabase();
        _repo = new AdvisorRepository(_db);
        _finra = new FinraService();
        _sec = new SecIapdService();
        _sync = new DataSyncService(_finra, _sec, _repo);

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
        _statusBar.Items.AddRange(new ToolStripItem[] { _lblStatus, _lblCount });
        this.Controls.Add(_statusBar);

        // Main split: filter | content
        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 220,
            Panel1MinSize = 180,
            Panel2MinSize = 400,
            FixedPanel = FixedPanel.Panel1
        };

        // Filter panel
        _filterPanel = new FilterPanel { Dock = DockStyle.Fill };
        _filterPanel.FiltersChanged += (_, _) => LoadAdvisors();
        _mainSplit.Panel1.Controls.Add(_filterPanel);

        // Content split: list | detail
        _contentSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 420,
            Panel1MinSize = 280
        };

        // Results list
        BuildListView();
        _contentSplit.Panel1.Controls.Add(_listView);

        // Detail card
        _detailCard = new AdvisorDetailCard { Dock = DockStyle.Fill };
        _detailCard.ExcludeRequested += OnExcludeRequested;
        _detailCard.RestoreRequested += OnRestoreRequested;
        _detailCard.ImportCrmRequested += OnImportCrmRequested;
        _detailCard.RefreshRequested += OnRefreshRequested;
        _contentSplit.Panel2.Controls.Add(_detailCard);

        _mainSplit.Panel2.Controls.Add(_contentSplit);
        this.Controls.Add(_mainSplit);
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

        _menuStrip.Items.AddRange(new ToolStripItem[] { dataMenu, crmMenu, viewMenu, helpMenu });
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
            Font = new Font("Segoe UI", 9),
            BorderStyle = BorderStyle.None
        };

        _listView.Columns.Add("Name", 180);
        _listView.Columns.Add("CRD", 70);
        _listView.Columns.Add("Firm", 180);
        _listView.Columns.Add("State", 50);
        _listView.Columns.Add("Status", 90);
        _listView.Columns.Add("Licenses", 100);
        _listView.Columns.Add("Disclosures", 85);
        _listView.Columns.Add("Source", 80);
        _listView.Columns.Add("Updated", 90);

        _listView.SelectedIndexChanged += OnListViewSelectionChanged;
        _listView.ColumnClick += OnColumnClick;

        // Context menu
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("View Details", null, (_, _) => { if (_selectedAdvisor != null) ShowAdvisorDetail(_selectedAdvisor.Id); });
        contextMenu.Items.Add("Refresh Data", null, (_, _) => { if (_selectedAdvisor != null) OnRefreshRequested(this, _selectedAdvisor); });
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Import to Wealthbox", null, (_, _) => { if (_selectedAdvisor != null) OnImportCrmRequested(this, _selectedAdvisor); });
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exclude from Results", null, (_, _) => { if (_selectedAdvisor != null) OnExcludeRequested(this, _selectedAdvisor); });
        contextMenu.Items.Add("Restore to Results", null, (_, _) => { if (_selectedAdvisor != null) OnRestoreRequested(this, _selectedAdvisor); });
        _listView.ContextMenuStrip = contextMenu;
    }

    private void LoadAdvisors()
    {
        try
        {
            var filter = _filterPanel.GetFilter();
            _currentAdvisors = _repo.GetAdvisors(filter);

            _listView.BeginUpdate();
            _listView.Items.Clear();

            foreach (var advisor in _currentAdvisors)
            {
                var item = new ListViewItem(advisor.FullName);
                item.SubItems.Add(advisor.CrdNumber ?? "");
                item.SubItems.Add(advisor.CurrentFirmName ?? "");
                item.SubItems.Add(advisor.State ?? "");
                item.SubItems.Add(advisor.RegistrationStatus ?? "");
                item.SubItems.Add(advisor.Licenses ?? "");
                item.SubItems.Add(advisor.HasDisclosures ? $"Yes ({advisor.DisclosureCount})" : "No");
                item.SubItems.Add(advisor.Source ?? "");
                item.SubItems.Add(advisor.UpdatedAt.ToString("yyyy-MM-dd"));
                item.Tag = advisor.Id;

                // Visual cues
                if (advisor.IsExcluded)
                {
                    item.ForeColor = Color.Gray;
                    item.Font = new Font("Segoe UI", 9, FontStyle.Strikeout);
                }
                else if (advisor.HasDisclosures)
                {
                    item.BackColor = Color.FromArgb(255, 250, 240);
                }
                if (advisor.IsImportedToCrm)
                {
                    item.ImageIndex = 0;
                }

                _listView.Items.Add(item);
            }

            _listView.EndUpdate();
            _lblCount.Text = $"{_currentAdvisors.Count} advisor{(_currentAdvisors.Count != 1 ? "s" : "")}";
            _lblStatus.Text = "Ready";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Error loading advisors: {ex.Message}";
        }
    }

    private void LoadFilterOptions()
    {
        try
        {
            var states = _repo.GetDistinctStates();
            _filterPanel.PopulateStates(states);
        }
        catch { /* ignore if DB not ready */ }
    }

    private void OnListViewSelectionChanged(object? sender, EventArgs e)
    {
        if (_listView.SelectedItems.Count == 0) return;
        if (_listView.SelectedItems[0].Tag is not int id) return;
        _selectedAdvisor = _currentAdvisors.FirstOrDefault(a => a.Id == id);

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
            1 => "CrdNumber",
            2 => "CurrentFirmName",
            3 => "State",
            4 => "RegistrationStatus",
            6 => "HasDisclosures",
            8 => "UpdatedAt",
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
        _db?.Dispose();
        base.OnFormClosed(e);
    }
}
