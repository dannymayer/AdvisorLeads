using AdvisorLeads.Models;
using AdvisorLeads.Services;

namespace AdvisorLeads.Controls;

public class DataCleaningPanel : UserControl
{
    private readonly DataCleaningService _service;
    private DataQualityReport? _report;

    private TabControl _tabs = null!;
    private Panel _summaryBar = null!;
    private Label _lblSummary = null!;
    private Button _btnAnalyze = null!;
    private Button _btnExport = null!;
    private ProgressBar _progressBar = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _lblStatus = null!;

    // Duplicates tab
    private ListView _lvDuplicates = null!;
    private Button _btnMergeDuplicate = null!;
    private Button _btnDismissDuplicate = null!;

    // Normalization tab
    private ListView _lvNormalization = null!;
    private Button _btnFixSelected = null!;
    private Button _btnFixAllAuto = null!;
    private ComboBox _cboSeverityFilter = null!;
    private ComboBox _cboEntityFilter = null!;

    // Orphans tab
    private ListView _lvOrphans = null!;
    private Button _btnDeleteSelected = null!;
    private Button _btnDeleteAll = null!;

    // Relationships tab
    private ListView _lvRelationships = null!;
    private Button _btnFixRelationship = null!;

    private readonly HashSet<int> _populatedTabs = new();

    public DataCleaningPanel(DataCleaningService service)
    {
        _service = service;
        BuildUI();
    }

    private void BuildUI()
    {
        this.Dock = DockStyle.Fill;
        this.Font = new Font("Segoe UI", 9);

        // Summary bar at top
        _summaryBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = Color.FromArgb(245, 247, 250)
        };

        _btnAnalyze = new Button
        {
            Text = "▶ Run Analysis",
            Width = 120,
            Height = 32,
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnAnalyze.Click += OnRunAnalysis;

        _btnExport = new Button
        {
            Text = "📄 Export Report",
            Width = 120,
            Height = 32,
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(80, 80, 80),
            ForeColor = Color.White,
            Enabled = false,
            Cursor = Cursors.Hand
        };
        _btnExport.Click += OnExportReport;

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Right,
            Width = 160,
            Height = 20,
            Style = ProgressBarStyle.Marquee,
            Visible = false,
            MarqueeAnimationSpeed = 30
        };

        _lblSummary = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Click \"Run Analysis\" to scan for data quality issues.",
            Font = new Font("Segoe UI", 10),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false
        };

        _summaryBar.Controls.AddRange(new Control[] { _lblSummary, _progressBar, _btnExport, _btnAnalyze });
        this.Controls.Add(_summaryBar);

        // Status strip
        _statusStrip = new StatusStrip();
        _lblStatus = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _statusStrip.Items.Add(_lblStatus);
        this.Controls.Add(_statusStrip);

        // Tab control
        _tabs = new TabControl { Dock = DockStyle.Fill };
        BuildDuplicatesTab();
        BuildNormalizationTab();
        BuildOrphansTab();
        BuildRelationshipsTab();
        this.Controls.Add(_tabs);

        // Ensure correct z-order: tabs fill center, summary on top, status on bottom
        _tabs.BringToFront();
        _tabs.SelectedIndexChanged += (_, _) => PopulateTab(_tabs.SelectedIndex);
    }

    // ── Duplicates Tab ────────────────────────────────────────────────

    private void BuildDuplicatesTab()
    {
        var tab = new TabPage("Duplicates");
        var panel = new Panel { Dock = DockStyle.Fill };

        _lvDuplicates = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            Font = new Font("Segoe UI", 9)
        };
        _lvDuplicates.Columns.Add("Names", 350);
        _lvDuplicates.Columns.Add("Reason", 180);
        _lvDuplicates.Columns.Add("Count", 60);
        _lvDuplicates.Columns.Add("Suggested Action", 300);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            Padding = new Padding(8, 4, 8, 4),
            FlowDirection = FlowDirection.RightToLeft
        };

        _btnDismissDuplicate = new Button { Text = "Dismiss", Width = 80, Enabled = false };

        _btnMergeDuplicate = new Button
        {
            Text = "Merge (Keep Best)",
            Width = 130,
            Enabled = false,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnMergeDuplicate.Click += OnMergeDuplicate;
        _lvDuplicates.SelectedIndexChanged += (_, _) =>
        {
            bool hasSelection = _lvDuplicates.SelectedItems.Count > 0;
            _btnMergeDuplicate.Enabled = hasSelection;
            _btnDismissDuplicate.Enabled = hasSelection;
        };

        toolbar.Controls.AddRange(new Control[] { _btnMergeDuplicate, _btnDismissDuplicate });
        panel.Controls.Add(_lvDuplicates);
        panel.Controls.Add(toolbar);
        tab.Controls.Add(panel);
        _tabs.TabPages.Add(tab);
    }

    // ── Normalization Tab ─────────────────────────────────────────────

    private void BuildNormalizationTab()
    {
        var tab = new TabPage("Normalization");
        var panel = new Panel { Dock = DockStyle.Fill };

        _lvNormalization = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            CheckBoxes = true,
            MultiSelect = true,
            Font = new Font("Segoe UI", 9)
        };
        _lvNormalization.Columns.Add("Entity", 70);
        _lvNormalization.Columns.Add("Name", 200);
        _lvNormalization.Columns.Add("Field", 100);
        _lvNormalization.Columns.Add("Current Value", 180);
        _lvNormalization.Columns.Add("Suggested Value", 180);
        _lvNormalization.Columns.Add("Severity", 70);

        var filterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(8, 6, 8, 2),
            FlowDirection = FlowDirection.LeftToRight
        };

        filterPanel.Controls.Add(new Label { Text = "Severity:", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
        _cboSeverityFilter = new ComboBox
        {
            Width = 100,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Items = { "All", "High", "Medium", "Low" }
        };
        _cboSeverityFilter.SelectedIndex = 0;
        _cboSeverityFilter.SelectedIndexChanged += (_, _) => RefreshNormalizationListView();
        filterPanel.Controls.Add(_cboSeverityFilter);

        filterPanel.Controls.Add(new Label { Text = "  Entity:", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
        _cboEntityFilter = new ComboBox
        {
            Width = 100,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Items = { "All", "Advisor", "Firm" }
        };
        _cboEntityFilter.SelectedIndex = 0;
        _cboEntityFilter.SelectedIndexChanged += (_, _) => RefreshNormalizationListView();
        filterPanel.Controls.Add(_cboEntityFilter);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            Padding = new Padding(8, 4, 8, 4),
            FlowDirection = FlowDirection.RightToLeft
        };

        _btnFixAllAuto = new Button
        {
            Text = "Fix All Auto-Fixable",
            Width = 140,
            Enabled = false,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnFixAllAuto.Click += OnFixAllAutoFixable;

        _btnFixSelected = new Button { Text = "Fix Selected", Width = 100, Enabled = false };
        _btnFixSelected.Click += OnFixSelected;
        _lvNormalization.ItemChecked += (_, _) =>
        {
            _btnFixSelected.Enabled = _lvNormalization.CheckedItems.Count > 0;
        };

        toolbar.Controls.AddRange(new Control[] { _btnFixAllAuto, _btnFixSelected });
        panel.Controls.Add(_lvNormalization);
        panel.Controls.Add(filterPanel);
        panel.Controls.Add(toolbar);
        tab.Controls.Add(panel);
        _tabs.TabPages.Add(tab);
    }

    // ── Orphans Tab ───────────────────────────────────────────────────

    private void BuildOrphansTab()
    {
        var tab = new TabPage("Orphaned Records");
        var panel = new Panel { Dock = DockStyle.Fill };

        _lvOrphans = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            CheckBoxes = true,
            MultiSelect = true,
            Font = new Font("Segoe UI", 9)
        };
        _lvOrphans.Columns.Add("Table", 130);
        _lvOrphans.Columns.Add("Record ID", 80);
        _lvOrphans.Columns.Add("Description", 500);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            Padding = new Padding(8, 4, 8, 4),
            FlowDirection = FlowDirection.RightToLeft
        };

        _btnDeleteAll = new Button
        {
            Text = "Delete All",
            Width = 100,
            Enabled = false,
            BackColor = Color.FromArgb(200, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnDeleteAll.Click += OnDeleteAllOrphans;

        _btnDeleteSelected = new Button { Text = "Delete Selected", Width = 120, Enabled = false };
        _btnDeleteSelected.Click += OnDeleteSelectedOrphans;
        _lvOrphans.ItemChecked += (_, _) =>
        {
            _btnDeleteSelected.Enabled = _lvOrphans.CheckedItems.Count > 0;
        };

        toolbar.Controls.AddRange(new Control[] { _btnDeleteAll, _btnDeleteSelected });
        panel.Controls.Add(_lvOrphans);
        panel.Controls.Add(toolbar);
        tab.Controls.Add(panel);
        _tabs.TabPages.Add(tab);
    }

    // ── Relationships Tab ─────────────────────────────────────────────

    private void BuildRelationshipsTab()
    {
        var tab = new TabPage("Relationships");
        var panel = new Panel { Dock = DockStyle.Fill };

        _lvRelationships = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            CheckBoxes = true,
            MultiSelect = true,
            Font = new Font("Segoe UI", 9)
        };
        _lvRelationships.Columns.Add("Advisor", 200);
        _lvRelationships.Columns.Add("Field", 120);
        _lvRelationships.Columns.Add("Current Value", 150);
        _lvRelationships.Columns.Add("Issue", 350);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            Padding = new Padding(8, 4, 8, 4),
            FlowDirection = FlowDirection.RightToLeft
        };

        _btnFixRelationship = new Button
        {
            Text = "Fix Selected",
            Width = 100,
            Enabled = false,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnFixRelationship.Click += OnFixRelationships;
        _lvRelationships.ItemChecked += (_, _) =>
        {
            _btnFixRelationship.Enabled = _lvRelationships.CheckedItems.Count > 0;
        };

        toolbar.Controls.Add(_btnFixRelationship);
        panel.Controls.Add(_lvRelationships);
        panel.Controls.Add(toolbar);
        tab.Controls.Add(panel);
        _tabs.TabPages.Add(tab);
    }

    // ── Event Handlers ────────────────────────────────────────────────

    private async void OnRunAnalysis(object? sender, EventArgs e)
    {
        _btnAnalyze.Enabled = false;
        _progressBar.Visible = true;
        _lblSummary.Text = "Analyzing data quality...";
        _lblStatus.Text = "Running analysis...";

        try
        {
            var progress = new Progress<string>(msg =>
            {
                if (InvokeRequired)
                    BeginInvoke(() => _lblStatus.Text = msg);
                else
                    _lblStatus.Text = msg;
            });

            _report = await Task.Run(() => _service.AnalyzeAsync(CancellationToken.None, progress));
            PopulateResults();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Analysis failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _lblStatus.Text = "Analysis failed.";
        }
        finally
        {
            _btnAnalyze.Enabled = true;
            _progressBar.Visible = false;
        }
    }

    private void PopulateResults()
    {
        if (_report == null) return;

        UpdateSummaryLabels();
        _populatedTabs.Clear();
        PopulateTab(_tabs.SelectedIndex);
    }

    private void UpdateSummaryLabels()
    {
        if (_report == null) return;

        var parts = new List<string>();
        if (_report.DuplicateAdvisorGroupCount > 0)
            parts.Add($"{_report.DuplicateAdvisorGroupCount} duplicate group(s)");
        if (_report.DuplicateFirmGroupCount > 0)
            parts.Add($"{_report.DuplicateFirmGroupCount} firm duplicate(s)");
        if (_report.NormalizationIssueCount > 0)
            parts.Add($"{_report.NormalizationIssueCount} normalization issue(s)");
        if (_report.OrphanedRecordCount > 0)
            parts.Add($"{_report.OrphanedRecordCount} orphaned record(s)");
        if (_report.InconsistentRelationshipCount > 0)
            parts.Add($"{_report.InconsistentRelationshipCount} relationship issue(s)");

        _lblSummary.Text = parts.Count > 0
            ? $"Found: {string.Join(" · ", parts)}  ({_report.TotalAdvisors:N0} advisors, {_report.TotalFirms:N0} firms)"
            : $"No issues found! ({_report.TotalAdvisors:N0} advisors, {_report.TotalFirms:N0} firms)";

        _btnExport.Enabled = true;
        _lblStatus.Text = $"Analysis complete in {_report.AnalysisDuration.TotalSeconds:F1}s — {_report.TotalIssues} total issues";
    }

    private void PopulateTab(int tabIndex)
    {
        if (_report == null || _populatedTabs.Contains(tabIndex)) return;
        _populatedTabs.Add(tabIndex);

        switch (tabIndex)
        {
            case 0: PopulateDuplicatesTab(); break;
            case 1:
                RefreshNormalizationListView();
                _btnFixAllAuto.Enabled = _report.NormalizationIssues.Any(i => i.IsAutoFixable);
                break;
            case 2: PopulateOrphansTab(); break;
            case 3: PopulateRelationshipsTab(); break;
        }
    }

    private void PopulateDuplicatesTab()
    {
        if (_report == null) return;

        var allDuplicates = _report.DuplicateAdvisors.Concat(_report.DuplicateFirms).ToList();
        _lvDuplicates.BeginUpdate();
        try
        {
            _lvDuplicates.Items.Clear();
            foreach (var g in allDuplicates)
            {
                var item = new ListViewItem(string.Join("; ", g.Names));
                item.SubItems.Add(g.Reason.ToString());
                item.SubItems.Add(g.EntityIds.Count.ToString());
                item.SubItems.Add($"Keep ID #{g.SuggestedKeepId}: {g.SuggestedKeepReason}");
                item.Tag = g;
                _lvDuplicates.Items.Add(item);
            }
        }
        finally
        {
            _lvDuplicates.EndUpdate();
        }
    }

    private void PopulateOrphansTab()
    {
        if (_report == null) return;

        _lvOrphans.BeginUpdate();
        try
        {
            _lvOrphans.Items.Clear();
            foreach (var issue in _report.OrphanedRecords)
            {
                var item = new ListViewItem(issue.EntityType.ToString());
                item.SubItems.Add(issue.EntityId.ToString());
                item.SubItems.Add(issue.Description);
                item.Tag = issue;
                _lvOrphans.Items.Add(item);
            }
        }
        finally
        {
            _lvOrphans.EndUpdate();
        }
        _btnDeleteAll.Enabled = _report.OrphanedRecords.Count > 0;
    }

    private void PopulateRelationshipsTab()
    {
        if (_report == null) return;

        _lvRelationships.BeginUpdate();
        try
        {
            _lvRelationships.Items.Clear();
            foreach (var issue in _report.InconsistentRelationships)
            {
                var item = new ListViewItem(issue.EntityName);
                item.SubItems.Add(issue.FieldName);
                item.SubItems.Add(issue.CurrentValue);
                item.SubItems.Add(issue.Description);
                item.Tag = issue;
                _lvRelationships.Items.Add(item);
            }
        }
        finally
        {
            _lvRelationships.EndUpdate();
        }
    }

    private void RefreshNormalizationListView()
    {
        if (_report == null) return;

        var issues = _report.NormalizationIssues.AsEnumerable();

        if (_cboSeverityFilter.SelectedIndex > 0)
        {
            var sev = (CleaningIssueSeverity)(_cboSeverityFilter.SelectedIndex - 1);
            issues = issues.Where(i => i.Severity == sev);
        }
        if (_cboEntityFilter.SelectedIndex > 0)
        {
            var ent = _cboEntityFilter.SelectedIndex == 1 ? CleaningEntityType.Advisor : CleaningEntityType.Firm;
            issues = issues.Where(i => i.EntityType == ent);
        }

        var filtered = issues.ToList();
        const int MaxDisplay = 2000;
        var display = filtered.Take(MaxDisplay).ToList();
        bool truncated = filtered.Count > MaxDisplay;

        _lvNormalization.BeginUpdate();
        try
        {
            _lvNormalization.Items.Clear();
            foreach (var issue in display)
            {
                var item = new ListViewItem(issue.EntityType.ToString());
                item.SubItems.Add(issue.EntityName);
                item.SubItems.Add(issue.FieldName);
                item.SubItems.Add(issue.CurrentValue);
                item.SubItems.Add(issue.SuggestedValue);
                item.SubItems.Add(issue.Severity.ToString());
                item.Tag = issue;
                if (issue.IsAutoFixable) item.ForeColor = Color.FromArgb(0, 100, 0);
                _lvNormalization.Items.Add(item);
            }

            if (truncated)
            {
                var note = new ListViewItem(new[]
                {
                    "", $"(showing {MaxDisplay:N0} of {filtered.Count:N0} issues — apply filters to narrow results)",
                    "", "", "", ""
                });
                note.ForeColor = Color.Gray;
                note.Font = new Font(_lvNormalization.Font, FontStyle.Italic);
                _lvNormalization.Items.Add(note);
            }
        }
        finally
        {
            _lvNormalization.EndUpdate();
        }

        _lblStatus.Text = truncated
            ? $"Showing {MaxDisplay:N0} of {filtered.Count:N0} normalization issues"
            : $"{filtered.Count:N0} normalization issues";
    }

    private async void OnMergeDuplicate(object? sender, EventArgs e)
    {
        if (_lvDuplicates.SelectedItems.Count == 0) return;
        var group = _lvDuplicates.SelectedItems[0].Tag as DuplicateGroup;
        if (group == null) return;

        if (group.EntityType != CleaningEntityType.Advisor)
        {
            MessageBox.Show("Firm merging is not supported yet.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Merge {group.EntityIds.Count} records, keeping ID #{group.SuggestedKeepId}?\n\n" +
            $"This will move all related data to the kept record and delete the duplicates.\n\n" +
            $"Records:\n{string.Join("\n", group.Names)}",
            "Confirm Merge", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (result != DialogResult.Yes) return;

        try
        {
            _lblStatus.Text = "Merging duplicates...";
            await Task.Run(() => _service.MergeDuplicateAdvisorsAsync(group, group.SuggestedKeepId, CancellationToken.None));
            _lvDuplicates.SelectedItems[0].Remove();
            _lblStatus.Text = "Merge complete.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Merge failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnFixSelected(object? sender, EventArgs e)
    {
        var issues = _lvNormalization.CheckedItems.Cast<ListViewItem>()
            .Select(i => i.Tag as CleaningIssue)
            .Where(i => i != null && !string.IsNullOrEmpty(i.SuggestedValue))
            .ToList();

        if (issues.Count == 0) return;

        var result = MessageBox.Show(
            $"Apply {issues.Count} normalization fix(es)?",
            "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes) return;

        try
        {
            _lblStatus.Text = "Applying fixes...";
            var progress = new Progress<string>(msg => _lblStatus.Text = msg);
            int count = await Task.Run(() => _service.ApplyNormalizationsAsync(issues!, CancellationToken.None, progress));
            _lblStatus.Text = $"Applied {count} fixes.";

            // Remove fixed items
            foreach (ListViewItem item in _lvNormalization.CheckedItems)
                item.Remove();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fix failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnFixAllAutoFixable(object? sender, EventArgs e)
    {
        if (_report == null) return;
        var autoFix = _report.NormalizationIssues.Where(i => i.IsAutoFixable && !string.IsNullOrEmpty(i.SuggestedValue)).ToList();
        if (autoFix.Count == 0) return;

        var result = MessageBox.Show(
            $"Apply all {autoFix.Count} auto-fixable normalization(s)?",
            "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes) return;

        try
        {
            _lblStatus.Text = "Applying auto-fixes...";
            var progress = new Progress<string>(msg => _lblStatus.Text = msg);
            int count = await Task.Run(() => _service.ApplyNormalizationsAsync(autoFix, CancellationToken.None, progress));
            _lblStatus.Text = $"Applied {count} auto-fixes.";
            _report.NormalizationIssues.RemoveAll(i => i.IsAutoFixable);
            _report.NormalizationIssueCount = _report.NormalizationIssues.Count;
            RefreshNormalizationListView();
            _btnFixAllAuto.Enabled = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Auto-fix failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnDeleteSelectedOrphans(object? sender, EventArgs e)
    {
        var issues = _lvOrphans.CheckedItems.Cast<ListViewItem>()
            .Select(i => i.Tag as CleaningIssue)
            .Where(i => i != null)
            .ToList();

        if (issues.Count == 0) return;

        var result = MessageBox.Show(
            $"Delete {issues.Count} orphaned record(s)? This cannot be undone.",
            "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;

        try
        {
            _lblStatus.Text = "Deleting orphans...";
            int count = await Task.Run(() => _service.DeleteOrphanedRecordsAsync(issues!, CancellationToken.None));
            _lblStatus.Text = $"Deleted {count} orphaned records.";

            foreach (ListViewItem item in _lvOrphans.CheckedItems)
                item.Remove();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Delete failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnDeleteAllOrphans(object? sender, EventArgs e)
    {
        if (_report == null) return;

        var result = MessageBox.Show(
            $"Delete ALL {_report.OrphanedRecords.Count} orphaned record(s)? This cannot be undone.",
            "Confirm Delete All", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;

        try
        {
            _lblStatus.Text = "Deleting all orphans...";
            int count = await Task.Run(() => _service.DeleteOrphanedRecordsAsync(_report.OrphanedRecords, CancellationToken.None));
            _lblStatus.Text = $"Deleted {count} orphaned records.";
            _lvOrphans.Items.Clear();
            _btnDeleteAll.Enabled = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Delete failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnFixRelationships(object? sender, EventArgs e)
    {
        var issues = _lvRelationships.CheckedItems.Cast<ListViewItem>()
            .Select(i => i.Tag as CleaningIssue)
            .Where(i => i != null && !string.IsNullOrEmpty(i.SuggestedValue))
            .ToList();

        if (issues.Count == 0)
        {
            MessageBox.Show("Only issues with a suggested value can be auto-fixed.", "Info",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Fix {issues.Count} relationship issue(s)?",
            "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes) return;

        try
        {
            _lblStatus.Text = "Fixing relationships...";
            var progress = new Progress<string>(msg => _lblStatus.Text = msg);
            int count = await Task.Run(() => _service.ApplyNormalizationsAsync(issues!, CancellationToken.None, progress));
            _lblStatus.Text = $"Fixed {count} relationship issues.";

            foreach (ListViewItem item in _lvRelationships.CheckedItems)
            {
                var issue = item.Tag as CleaningIssue;
                if (issue != null && !string.IsNullOrEmpty(issue.SuggestedValue))
                    item.Remove();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fix failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnExportReport(object? sender, EventArgs e)
    {
        if (_report == null) return;

        using var dlg = new SaveFileDialog
        {
            Title = "Export Cleaning Report",
            Filter = "JSON files (*.json)|*.json|CSV files (*.csv)|*.csv",
            FileName = $"DataQualityReport_{DateTime.Now:yyyyMMdd_HHmmss}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            if (dlg.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(_report, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(dlg.FileName, json);
            }
            else
            {
                ExportAsCsv(dlg.FileName);
            }
            _lblStatus.Text = $"Report exported to {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportAsCsv(string path)
    {
        if (_report == null) return;

        using var writer = new StreamWriter(path);
        writer.WriteLine("Category,EntityType,EntityId,EntityName,Field,CurrentValue,SuggestedValue,Severity,Description");

        void WriteIssue(string category, CleaningIssue issue)
        {
            writer.WriteLine($"\"{category}\",\"{issue.EntityType}\",{issue.EntityId},\"{Escape(issue.EntityName)}\",\"{issue.FieldName}\",\"{Escape(issue.CurrentValue)}\",\"{Escape(issue.SuggestedValue)}\",\"{issue.Severity}\",\"{Escape(issue.Description)}\"");
        }

        foreach (var g in _report.DuplicateAdvisors.Concat(_report.DuplicateFirms))
            writer.WriteLine($"\"Duplicate\",\"{g.EntityType}\",\"{string.Join(";", g.EntityIds)}\",\"{Escape(string.Join("; ", g.Names))}\",\"\",\"\",\"\",\"\",\"{g.Reason}: Keep #{g.SuggestedKeepId}\"");

        foreach (var i in _report.NormalizationIssues) WriteIssue("Normalization", i);
        foreach (var i in _report.OrphanedRecords) WriteIssue("Orphaned", i);
        foreach (var i in _report.InconsistentRelationships) WriteIssue("Relationship", i);

        static string Escape(string s) => s.Replace("\"", "\"\"");
    }
}
