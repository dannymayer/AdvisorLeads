using AdvisorLeads.Data;
using AdvisorLeads.Forms;
using AdvisorLeads.Models;

namespace AdvisorLeads.Controls;

public class AlertsPanel : UserControl
{
    private readonly AlertRepository _alertRepo;
    private readonly Func<string, string?> _loadSetting;
    private readonly Action<string, string> _saveSetting;
    private readonly Func<string, string?>? _firmNameLookup;

    private List<AlertLog> _alerts = new();
    private bool _splitterSet;

    // Alert log controls
    private ListView _alertListView = null!;
    private ToolStripComboBox _cmbFilter = null!;

    // AUM Rules
    private ListView _aumRulesListView = null!;
    private Button _btnAddAumRule = null!;
    private Button _btnEditAumRule = null!;
    private Button _btnDeleteAumRule = null!;

    // Market Watch
    private ListView _watchRulesListView = null!;
    private Button _btnAddWatchRule = null!;
    private Button _btnEditWatchRule = null!;
    private Button _btnDeleteWatchRule = null!;

    // Settings
    private CheckBox _chkToastNotifications = null!;
    private CheckBox _chkSoundAlerts = null!;

    public event EventHandler<AlertLog>? OpenAdvisorRequested;
    public event EventHandler<AlertLog>? OpenFirmRequested;

    public int UnreadCount => _alertRepo.GetUnreadCount();

    public AlertsPanel(
        AlertRepository repo,
        Func<string, string?> loadSetting,
        Action<string, string> saveSetting,
        Func<string, string?>? firmNameLookup = null)
    {
        _alertRepo = repo;
        _loadSetting = loadSetting;
        _saveSetting = saveSetting;
        _firmNameLookup = firmNameLookup;
        BuildUI();
        LoadSettings();
    }

    private void BuildUI()
    {
        this.Dock = DockStyle.Fill;
        this.BackColor = Color.White;

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel2,
            SplitterWidth = 5
        };

        BuildAlertLogPanel(split.Panel1);
        BuildRulesPanel(split.Panel2);

        // Defer Panel2MinSize and splitter distance until the control has actual width
        split.SizeChanged += (_, _) =>
        {
            try { split.Panel2MinSize = 350; } catch { /* ignore until layout is established */ }
            if (_splitterSet || split.Width <= 400) return;
            _splitterSet = true;
            try { split.SplitterDistance = Math.Max(200, split.Width - 350); }
            catch { /* ignore layout timing issues */ }
        };

        this.Controls.Add(split);
    }

    // ── Alert Log ───────────────────────────────────────────────────────────

    private void BuildAlertLogPanel(SplitterPanel panel)
    {
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };

        var btnMarkAll = new ToolStripButton("Mark All Read") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var btnClear = new ToolStripButton("Clear Acknowledged") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        var btnRefresh = new ToolStripButton("Refresh") { DisplayStyle = ToolStripItemDisplayStyle.Text };

        _cmbFilter = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
        _cmbFilter.Items.AddRange(new object[] { "All", "High", "Medium", "Low", "Unread" });
        _cmbFilter.SelectedIndex = 0;

        toolbar.Items.Add(btnMarkAll);
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(btnClear);
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(btnRefresh);
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripLabel("Filter:"));
        toolbar.Items.Add(_cmbFilter);

        btnMarkAll.Click += (_, _) => { _alertRepo.MarkAllRead(); RefreshAlerts(); };
        btnClear.Click += (_, _) => { _alertRepo.PruneOldAlerts(DateTime.UtcNow); RefreshAlerts(); };
        btnRefresh.Click += (_, _) => RefreshAlerts();
        _cmbFilter.SelectedIndexChanged += (_, _) => ApplyFilter();

        _alertListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 9),
            BorderStyle = BorderStyle.None
        };
        _alertListView.Columns.Add("!", 24);
        _alertListView.Columns.Add("Time", 100);
        _alertListView.Columns.Add("Type", 120);
        _alertListView.Columns.Add("Entity", 150);
        _alertListView.Columns.Add("Summary", -2);

        _alertListView.ContextMenuStrip = BuildAlertContextMenu();

        panel.Controls.Add(_alertListView);
        panel.Controls.Add(toolbar);
    }

    private ContextMenuStrip BuildAlertContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Mark as Read", null, (_, _) =>
        {
            var a = GetSelectedAlert();
            if (a != null) { _alertRepo.MarkRead(a.Id); RefreshAlerts(); }
        });
        menu.Items.Add("Acknowledge", null, (_, _) =>
        {
            var a = GetSelectedAlert();
            if (a != null) { _alertRepo.Acknowledge(a.Id); RefreshAlerts(); }
        });
        menu.Items.Add("-");
        menu.Items.Add("Open in Advisor view", null, (_, _) =>
        {
            var a = GetSelectedAlert();
            if (a?.EntityType == "Advisor") OpenAdvisorRequested?.Invoke(this, a);
        });
        menu.Items.Add("Open in Firm view", null, (_, _) =>
        {
            var a = GetSelectedAlert();
            if (a?.EntityType == "Firm") OpenFirmRequested?.Invoke(this, a);
        });
        return menu;
    }

    private AlertLog? GetSelectedAlert()
    {
        if (_alertListView.SelectedItems.Count == 0) return null;
        return _alertListView.SelectedItems[0].Tag as AlertLog;
    }

    // ── Rules panels ────────────────────────────────────────────────────────

    private void BuildRulesPanel(SplitterPanel panel)
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };

        var tabAum = new TabPage("AUM Rules");
        tabAum.Controls.Add(BuildAumRulesContent());

        var tabWatch = new TabPage("Market Watch");
        tabWatch.Controls.Add(BuildMarketWatchContent());

        var tabSettings = new TabPage("Settings");
        tabSettings.Controls.Add(BuildSettingsContent());

        tabs.TabPages.AddRange(new[] { tabAum, tabWatch, tabSettings });
        panel.Controls.Add(tabs);
    }

    private Panel BuildAumRulesContent()
    {
        var container = new Panel { Dock = DockStyle.Fill };

        _aumRulesListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 9),
            BorderStyle = BorderStyle.None
        };
        _aumRulesListView.Columns.Add("Firm Name", 200);
        _aumRulesListView.Columns.Add("CRD", 80);
        _aumRulesListView.Columns.Add("Direction", 80);
        _aumRulesListView.Columns.Add("Threshold AUM", 120);
        _aumRulesListView.Columns.Add("Pct Change", 80);

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(4)
        };
        _btnAddAumRule = MakeSmallButton("Add");
        _btnEditAumRule = MakeSmallButton("Edit");
        _btnDeleteAumRule = MakeSmallButton("Delete");

        _btnAddAumRule.Click += OnAddAumRule;
        _btnEditAumRule.Click += OnEditAumRule;
        _btnDeleteAumRule.Click += OnDeleteAumRule;

        btnPanel.Controls.AddRange(new Control[] { _btnAddAumRule, _btnEditAumRule, _btnDeleteAumRule });
        container.Controls.Add(_aumRulesListView);
        container.Controls.Add(btnPanel);
        return container;
    }

    private Panel BuildMarketWatchContent()
    {
        var container = new Panel { Dock = DockStyle.Fill };

        _watchRulesListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 9),
            BorderStyle = BorderStyle.None
        };
        _watchRulesListView.Columns.Add("Rule Name", 180);
        _watchRulesListView.Columns.Add("State", 60);
        _watchRulesListView.Columns.Add("Record Type", 120);
        _watchRulesListView.Columns.Add("Min Exp.", 60);
        _watchRulesListView.Columns.Add("Status", 60);

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(4)
        };
        _btnAddWatchRule = MakeSmallButton("Add");
        _btnEditWatchRule = MakeSmallButton("Edit");
        _btnDeleteWatchRule = MakeSmallButton("Delete");

        _btnAddWatchRule.Click += OnAddWatchRule;
        _btnEditWatchRule.Click += OnEditWatchRule;
        _btnDeleteWatchRule.Click += OnDeleteWatchRule;

        btnPanel.Controls.AddRange(new Control[] { _btnAddWatchRule, _btnEditWatchRule, _btnDeleteWatchRule });
        container.Controls.Add(_watchRulesListView);
        container.Controls.Add(btnPanel);
        return container;
    }

    private Control BuildSettingsContent()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _chkToastNotifications = new CheckBox
        {
            Text = "Show Windows toast notifications when alerts arrive",
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 8)
        };
        _chkSoundAlerts = new CheckBox
        {
            Text = "Play sound on High severity alerts",
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 16)
        };
        var btnSave = new Button
        {
            Text = "Save Settings",
            Width = 130,
            Height = 30,
            BackColor = Color.FromArgb(0, 100, 160),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += OnSaveSettings;

        layout.Controls.Add(_chkToastNotifications, 0, 0);
        layout.Controls.Add(_chkSoundAlerts, 0, 1);
        layout.Controls.Add(btnSave, 0, 2);
        return layout;
    }

    // ── Settings persistence ─────────────────────────────────────────────────

    private void LoadSettings()
    {
        _chkToastNotifications.Checked = _loadSetting("AlertToastEnabled") == "true";
        _chkSoundAlerts.Checked = _loadSetting("AlertSoundEnabled") == "true";
    }

    private void OnSaveSettings(object? sender, EventArgs e)
    {
        _saveSetting("AlertToastEnabled", _chkToastNotifications.Checked ? "true" : "false");
        _saveSetting("AlertSoundEnabled", _chkSoundAlerts.Checked ? "true" : "false");
        MessageBox.Show("Settings saved.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void RefreshAlerts()
    {
        try { _alerts = _alertRepo.GetRecentAlerts(limit: 1000); }
        catch { _alerts = new List<AlertLog>(); }

        ApplyFilter();
        RefreshAumRules();
        RefreshWatchRules();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        string filter = _cmbFilter.SelectedItem?.ToString() ?? "All";
        var cutoff = DateTime.UtcNow.AddDays(-30);
        IEnumerable<AlertLog> visible = _alerts.Where(a => a.DetectedAt >= cutoff);

        visible = filter switch
        {
            "High"   => visible.Where(a => a.Severity == "High"),
            "Medium" => visible.Where(a => a.Severity == "Medium"),
            "Low"    => visible.Where(a => a.Severity == "Low"),
            "Unread" => visible.Where(a => !a.IsRead),
            _        => visible
        };

        _alertListView.BeginUpdate();
        _alertListView.Items.Clear();
        foreach (var alert in visible)
        {
            string icon = alert.Severity switch
            {
                "High"   => "▲",
                "Medium" => "●",
                _        => "○"
            };
            var item = new ListViewItem(icon);
            item.SubItems.Add(alert.DetectedAt.ToLocalTime().ToString("MM/dd HH:mm"));
            item.SubItems.Add(alert.AlertType);
            item.SubItems.Add(alert.EntityName ?? alert.EntityCrd);
            item.SubItems.Add(alert.Summary);
            item.Tag = alert;

            item.BackColor = alert.Severity switch
            {
                "High"   => Color.FromArgb(255, 230, 230),
                "Medium" => Color.FromArgb(255, 243, 224),
                _        => Color.FromArgb(232, 240, 254)
            };

            if (alert.IsAcknowledged)
                item.ForeColor = Color.FromArgb(160, 160, 160);
            else if (!alert.IsRead)
                item.Font = new Font(_alertListView.Font, FontStyle.Bold);

            _alertListView.Items.Add(item);
        }
        _alertListView.EndUpdate();
    }

    private void RefreshAumRules()
    {
        try
        {
            var rules = _alertRepo.GetActiveAumRules();
            _aumRulesListView.BeginUpdate();
            _aumRulesListView.Items.Clear();
            foreach (var rule in rules)
            {
                var item = new ListViewItem(rule.FirmName ?? "");
                item.SubItems.Add(rule.FirmCrd);
                item.SubItems.Add(rule.ThresholdType);
                item.SubItems.Add(FormatAum(rule.ThresholdAmount));
                item.SubItems.Add("—");   // FirmAumAlertRule has no PctChange field
                item.Tag = rule;
                _aumRulesListView.Items.Add(item);
            }
            _aumRulesListView.EndUpdate();
        }
        catch { /* non-critical */ }
    }

    private void RefreshWatchRules()
    {
        try
        {
            var rules = _alertRepo.GetActiveMarketWatchRules();
            _watchRulesListView.BeginUpdate();
            _watchRulesListView.Items.Clear();
            foreach (var rule in rules)
            {
                var item = new ListViewItem(rule.RuleName);
                item.SubItems.Add(rule.State ?? "Any");
                item.SubItems.Add(rule.RecordType ?? "Any");
                item.SubItems.Add(rule.MinYearsExperience?.ToString() ?? "—");
                item.SubItems.Add(rule.IsActive ? "Active" : "Paused");
                item.Tag = rule;
                _watchRulesListView.Items.Add(item);
            }
            _watchRulesListView.EndUpdate();
        }
        catch { /* non-critical */ }
    }

    // ── AUM Rule handlers ────────────────────────────────────────────────────

    private void OnAddAumRule(object? sender, EventArgs e)
    {
        using var dlg = new FirmAumAlertRuleDialog(_alertRepo, null, _firmNameLookup);
        if (dlg.ShowDialog(this) == DialogResult.OK)
            RefreshAumRules();
    }

    private void OnEditAumRule(object? sender, EventArgs e)
    {
        if (_aumRulesListView.SelectedItems.Count == 0) return;
        if (_aumRulesListView.SelectedItems[0].Tag is not FirmAumAlertRule rule) return;
        using var dlg = new FirmAumAlertRuleDialog(_alertRepo, rule, _firmNameLookup);
        if (dlg.ShowDialog(this) == DialogResult.OK)
            RefreshAumRules();
    }

    private void OnDeleteAumRule(object? sender, EventArgs e)
    {
        if (_aumRulesListView.SelectedItems.Count == 0) return;
        if (_aumRulesListView.SelectedItems[0].Tag is not FirmAumAlertRule rule) return;
        if (MessageBox.Show($"Delete AUM rule for '{rule.FirmName ?? rule.FirmCrd}'?",
            "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _alertRepo.DeleteAumRule(rule.Id);
        RefreshAumRules();
    }

    // ── Market Watch handlers ────────────────────────────────────────────────

    private void OnAddWatchRule(object? sender, EventArgs e)
    {
        using var dlg = new MarketWatchRuleDialog(_alertRepo, null);
        if (dlg.ShowDialog(this) == DialogResult.OK)
            RefreshWatchRules();
    }

    private void OnEditWatchRule(object? sender, EventArgs e)
    {
        if (_watchRulesListView.SelectedItems.Count == 0) return;
        if (_watchRulesListView.SelectedItems[0].Tag is not MarketWatchRule rule) return;
        using var dlg = new MarketWatchRuleDialog(_alertRepo, rule);
        if (dlg.ShowDialog(this) == DialogResult.OK)
            RefreshWatchRules();
    }

    private void OnDeleteWatchRule(object? sender, EventArgs e)
    {
        if (_watchRulesListView.SelectedItems.Count == 0) return;
        if (_watchRulesListView.SelectedItems[0].Tag is not MarketWatchRule rule) return;
        if (MessageBox.Show($"Delete rule '{rule.RuleName}'?",
            "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _alertRepo.DeleteMarketWatchRule(rule.Id);
        RefreshWatchRules();
    }

    // ── Utilities ────────────────────────────────────────────────────────────

    private static Button MakeSmallButton(string text) => new Button
    {
        Text = text,
        Width = 70,
        Height = 26,
        BackColor = Color.FromArgb(70, 130, 180),
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 9),
        Margin = new Padding(0, 0, 6, 0)
    };

    private static string FormatAum(decimal aum)
    {
        if (aum >= 1_000_000_000m) return $"${aum / 1_000_000_000m:F1}B";
        if (aum >= 1_000_000m) return $"${aum / 1_000_000m:F0}M";
        if (aum >= 1_000m) return $"${aum / 1_000m:F0}K";
        return $"${aum:F0}";
    }
}
