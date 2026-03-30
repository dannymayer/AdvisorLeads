using AdvisorLeads.Data;
using AdvisorLeads.Models;
using AdvisorLeads.Services;

namespace AdvisorLeads.Forms;

public class ListManagerForm : Form
{
    private readonly ListRepository _listRepo;
    private readonly AdvisorRepository _advisorRepo;
    private readonly WealthboxService? _wealthbox;
    private readonly Action<int>? _onAdvisorImported;

    private ListBox _lstLists = null!;
    private ListView _lvMembers = null!;
    private Button _btnNewList = null!;
    private Button _btnRenameList = null!;
    private Button _btnDeleteList = null!;
    private Button _btnRemoveMember = null!;
    private Button _btnExportCsv = null!;
    private Button _btnExportAdvanced = null!;
    private Button _btnExportCrm = null!;
    private Button _btnClose = null!;
    private Label _lblListInfo = null!;
    private StatusStrip _status = null!;
    private ToolStripStatusLabel _lblStatus = null!;

    private List<AdvisorList> _allLists = new();
    private List<Advisor> _currentMembers = new();

    public ListManagerForm(ListRepository listRepo, AdvisorRepository advisorRepo, WealthboxService? wealthbox, Action<int>? onAdvisorImported = null)
    {
        _listRepo = listRepo;
        _advisorRepo = advisorRepo;
        _wealthbox = wealthbox;
        _onAdvisorImported = onAdvisorImported;
        BuildUI();
        LoadLists();
    }

    private void BuildUI()
    {
        Text = "Manage Lists";
        Size = new Size(1000, 680);
        MinimumSize = new Size(700, 450);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9);

        _status = new StatusStrip();
        _lblStatus = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _status.Items.Add(_lblStatus);
        Controls.Add(_status);

        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel1,
            SplitterDistance = 220
        };

        // ── Left: list-of-lists ────────────────────────────────────────
        var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        var lblListsHeader = new Label
        {
            Text = "My Lists",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Color.FromArgb(50, 50, 120)
        };

        _lstLists = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9),
            SelectionMode = SelectionMode.One,
            BorderStyle = BorderStyle.FixedSingle
        };
        _lstLists.SelectedIndexChanged += OnListSelected;

        var listBtnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            MinimumSize = new Size(0, 36),
            Padding = new Padding(0, 4, 0, 0)
        };

        _btnNewList = MakeButton("+ New", Color.FromArgb(60, 130, 60), 65);
        _btnRenameList = MakeButton("✏ Edit", Color.FromArgb(70, 100, 180), 65);
        _btnDeleteList = MakeButton("🗑 Delete", Color.FromArgb(200, 60, 60), 75);
        _btnNewList.Click += OnNewList;
        _btnRenameList.Click += OnRenameList;
        _btnDeleteList.Click += OnDeleteList;
        listBtnPanel.Controls.AddRange(new Control[] { _btnNewList, _btnRenameList, _btnDeleteList });

        leftPanel.Controls.Add(_lstLists);
        leftPanel.Controls.Add(lblListsHeader);
        leftPanel.Controls.Add(listBtnPanel);
        mainSplit.Panel1.Controls.Add(leftPanel);

        // ── Right: list members ────────────────────────────────────────
        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 8, 8, 0) };

        _lblListInfo = new Label
        {
            Text = "Select a list to view members",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = Color.Gray
        };

        _lvMembers = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = true,
            Font = new Font("Segoe UI", 9)
        };
        _lvMembers.Columns.Add("Name", -2);
        _lvMembers.Columns.Add("Type", 100);
        _lvMembers.Columns.Add("CRD", 75);
        _lvMembers.Columns.Add("Firm", -2);
        _lvMembers.Columns.Add("City", 100);
        _lvMembers.Columns.Add("State", 50);
        _lvMembers.Columns.Add("Status", 90);
        _lvMembers.Columns.Add("Exp.", 45);
        _lvMembers.Columns.Add("Disclosures", 90);
        _lvMembers.Columns.Add("CRM", 60);
        _lvMembers.SelectedIndexChanged += (_, _) => UpdateButtonStates();

        // Context menu for the members list
        var ctx = new ContextMenuStrip();
        ctx.Items.Add("Remove from List", null, (_, _) => OnRemoveMember(null, EventArgs.Empty));
        ctx.Items.Add("-");
        ctx.Items.Add("Export Selected to CSV", null, (_, _) => ExportSelectedToCsv());
        ctx.Items.Add("Import Selected to Wealthbox", null, async (_, _) => await ImportSelectedToCrm());
        _lvMembers.ContextMenuStrip = ctx;

        var memberBtnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            MinimumSize = new Size(0, 36),
            Padding = new Padding(0, 4, 0, 4)
        };
        _btnRemoveMember = MakeButton("Remove Selected", Color.FromArgb(200, 80, 60), 120);
        _btnExportCsv = MakeButton("Export CSV", Color.FromArgb(60, 140, 60), 100);
        _btnExportAdvanced = MakeButton("Export…", Color.FromArgb(40, 120, 160), 80);
        _btnExportCrm = MakeButton("Export to Wealthbox", Color.FromArgb(130, 80, 170), 140);
        _btnClose = MakeButton("Close", Color.FromArgb(130, 130, 130), 70);
        _btnRemoveMember.Click += OnRemoveMember;
        _btnExportCsv.Click += (_, _) => ExportListToCsv();
        _btnExportAdvanced.Click += (_, _) => OnExportAdvanced();
        _btnExportCrm.Click += async (_, _) => await ImportListToCrm();
        _btnClose.Click += (_, _) => Close();
        memberBtnPanel.Controls.AddRange(new Control[] { _btnRemoveMember, _btnExportCsv, _btnExportAdvanced, _btnExportCrm, _btnClose });

        rightPanel.Controls.Add(_lvMembers);
        rightPanel.Controls.Add(_lblListInfo);
        rightPanel.Controls.Add(memberBtnPanel);
        mainSplit.Panel2.Controls.Add(rightPanel);

        Controls.Add(mainSplit);
        UpdateButtonStates();
    }

    private void LoadLists()
    {
        _allLists = _listRepo.GetAllLists();
        var selected = _lstLists.SelectedItem?.ToString();
        _lstLists.Items.Clear();
        foreach (var l in _allLists)
            _lstLists.Items.Add($"{l.Name}  ({l.MemberCount})");
        var idx = selected != null
            ? _lstLists.Items.Cast<string>().ToList().FindIndex(s => s.StartsWith(selected.Split(' ')[0]))
            : -1;
        _lstLists.SelectedIndex = idx >= 0 ? idx : (_lstLists.Items.Count > 0 ? 0 : -1);
        if (_lstLists.SelectedIndex < 0)
        {
            _lvMembers.Items.Clear();
            _currentMembers.Clear();
            _lblListInfo.Text = "No lists yet. Click '+ New' to create one.";
        }
    }

    private void OnListSelected(object? sender, EventArgs e)
    {
        if (_lstLists.SelectedIndex < 0 || _lstLists.SelectedIndex >= _allLists.Count) return;
        var list = _allLists[_lstLists.SelectedIndex];
        _lblListInfo.Text = $"List: {list.Name}  ·  {list.MemberCount} member{(list.MemberCount != 1 ? "s" : "")}";
        if (!string.IsNullOrEmpty(list.Description))
            _lblListInfo.Text += $"  ·  {list.Description}";
        LoadMembers(list.Id);
        UpdateButtonStates();
    }

    private void LoadMembers(int listId)
    {
        _currentMembers = _listRepo.GetListMembers(listId);
        _lvMembers.BeginUpdate();
        _lvMembers.Items.Clear();
        foreach (var a in _currentMembers)
        {
            var item = new ListViewItem(a.FullName);
            item.SubItems.Add(a.RecordType ?? "");
            item.SubItems.Add(a.CrdNumber ?? "");
            item.SubItems.Add(a.CurrentFirmName ?? "");
            item.SubItems.Add(a.City ?? "");
            item.SubItems.Add(a.State ?? "");
            item.SubItems.Add(a.RegistrationStatus ?? "");
            item.SubItems.Add(a.YearsOfExperience?.ToString() ?? "");
            item.SubItems.Add(a.HasDisclosures ? $"Yes ({a.DisclosureCount})" : "No");
            item.SubItems.Add(a.IsImportedToCrm ? "✓" : "");
            item.Tag = a.Id;
            if (a.HasDisclosures) item.BackColor = Color.FromArgb(255, 250, 240);
            if (a.IsImportedToCrm) item.ForeColor = Color.FromArgb(100, 60, 160);
            _lvMembers.Items.Add(item);
        }
        _lvMembers.EndUpdate();
    }

    private void OnNewList(object? sender, EventArgs e)
    {
        using var dlg = new CreateListDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _listRepo.CreateList(dlg.ListName, dlg.ListDescription);
            LoadLists();
        }
    }

    private void OnRenameList(object? sender, EventArgs e)
    {
        if (_lstLists.SelectedIndex < 0 || _lstLists.SelectedIndex >= _allLists.Count) return;
        var list = _allLists[_lstLists.SelectedIndex];
        using var dlg = new CreateListDialog(list.Name, list.Description);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _listRepo.RenameList(list.Id, dlg.ListName, dlg.ListDescription);
            LoadLists();
        }
    }

    private void OnDeleteList(object? sender, EventArgs e)
    {
        if (_lstLists.SelectedIndex < 0 || _lstLists.SelectedIndex >= _allLists.Count) return;
        var list = _allLists[_lstLists.SelectedIndex];
        var result = MessageBox.Show(
            $"Delete list \"{list.Name}\" and remove all {list.MemberCount} member(s)?\n\nThis does NOT delete the advisors from the database.",
            "Delete List", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result == DialogResult.Yes)
        {
            _listRepo.DeleteList(list.Id);
            LoadLists();
        }
    }

    private void OnRemoveMember(object? sender, EventArgs e)
    {
        if (_lstLists.SelectedIndex < 0 || _lstLists.SelectedIndex >= _allLists.Count) return;
        var list = _allLists[_lstLists.SelectedIndex];
        var selectedIds = _lvMembers.SelectedItems.Cast<ListViewItem>()
            .Where(i => i.Tag is int)
            .Select(i => (int)i.Tag!).ToList();
        if (selectedIds.Count == 0) return;

        var confirmed = MessageBox.Show(
            $"Remove {selectedIds.Count} advisor(s) from \"{list.Name}\"?",
            "Remove from List", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirmed == DialogResult.Yes)
        {
            foreach (var advisorId in selectedIds)
                _listRepo.RemoveFromList(list.Id, advisorId);
            LoadLists();
            OnListSelected(null, EventArgs.Empty);
        }
    }

    private void ExportListToCsv()
    {
        if (!_currentMembers.Any())
        {
            MessageBox.Show("The list is empty.", "Nothing to Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ExportToCsv(_currentMembers);
    }

    private void ExportSelectedToCsv()
    {
        var selected = _lvMembers.SelectedItems.Cast<ListViewItem>()
            .Where(i => i.Tag is int)
            .Select(i => _currentMembers.FirstOrDefault(a => a.Id == (int)i.Tag!))
            .Where(a => a != null).Cast<Advisor>().ToList();
        if (!selected.Any()) { ExportListToCsv(); return; }
        ExportToCsv(selected);
    }

    private void ExportToCsv(List<Advisor> advisors)
    {
        using var sfd = new SaveFileDialog
        {
            Title = "Export to CSV",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            DefaultExt = "csv",
            FileName = GetSelectedListName() + "_export"
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var sw = new StreamWriter(sfd.FileName, false, System.Text.Encoding.UTF8);
            sw.WriteLine("CRD,First Name,Middle Name,Last Name,Other Names,Record Type,Firm,Firm CRD,City,State,Zip,Email,Phone,Registration Status,Registration Date,Years Experience,Has Disclosures,Disclosure Count,Source,Is Imported to CRM,CRM ID,Licenses,Updated At");

            foreach (var a in advisors)
            {
                sw.WriteLine(string.Join(",",
                    CsvEscape(a.CrdNumber),
                    CsvEscape(a.FirstName),
                    CsvEscape(a.MiddleName),
                    CsvEscape(a.LastName),
                    CsvEscape(a.OtherNames),
                    CsvEscape(a.RecordType),
                    CsvEscape(a.CurrentFirmName),
                    CsvEscape(a.CurrentFirmCrd),
                    CsvEscape(a.City),
                    CsvEscape(a.State),
                    CsvEscape(a.ZipCode),
                    CsvEscape(a.Email),
                    CsvEscape(a.Phone),
                    CsvEscape(a.RegistrationStatus),
                    CsvEscape(a.RegistrationDate?.ToString("yyyy-MM-dd")),
                    CsvEscape(a.YearsOfExperience?.ToString()),
                    CsvEscape(a.HasDisclosures ? "Yes" : "No"),
                    CsvEscape(a.DisclosureCount.ToString()),
                    CsvEscape(a.Source),
                    CsvEscape(a.IsImportedToCrm ? "Yes" : "No"),
                    CsvEscape(a.CrmId),
                    CsvEscape(a.Licenses),
                    CsvEscape(a.UpdatedAt.ToString("yyyy-MM-dd"))
                ));
            }
            _lblStatus.Text = $"Exported {advisors.Count} record(s) to {Path.GetFileName(sfd.FileName)}";
            MessageBox.Show($"Exported {advisors.Count} record(s).", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string CsvEscape(string? value)
    {
        if (value == null) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private void OnExportAdvanced()
    {
        if (!_currentMembers.Any())
        {
            MessageBox.Show("The list is empty.", "Nothing to Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var allKeys = AdvisorExportColumns.All.Select(c => c.Key).ToList();
        var allHeaders = AdvisorExportColumns.All.Select(c => c.Header).ToList();
        var defaultKeys = AdvisorExportColumns.GetPreset("Default").Select(c => c.Key).ToList();

        var selected = _lvMembers.SelectedItems.Cast<ListViewItem>()
            .Where(i => i.Tag is int)
            .Select(i => _currentMembers.FirstOrDefault(a => a.Id == (int)i.Tag!))
            .Where(a => a != null).Cast<Advisor>().ToList();

        using var dlg = new ExportDialog(
            title: "Export List Members",
            allColumnKeys: allKeys,
            allColumnHeaders: allHeaders,
            selectedKeys: defaultKeys,
            presetNames: AdvisorExportColumns.PresetNames,
            loadSetting: _ => null,
            saveSetting: (_, _) => { },
            totalRecords: _currentMembers.Count,
            selectedCount: selected.Count,
            entityType: "Advisor");

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var selectedColumns = AdvisorExportColumns.All
            .Where(c => dlg.SelectedKeys.Contains(c.Key))
            .OrderBy(c => dlg.SelectedKeys.IndexOf(c.Key))
            .ToList();

        var records = dlg.ExportAllRecords ? _currentMembers : selected;

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
            _lblStatus.Text = $"Exported {records.Count} record(s) to {Path.GetFileName(dlg.ChosenFilePath)}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.ChosenFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ImportListToCrm()
    {
        if (!_currentMembers.Any())
        {
            MessageBox.Show("The list is empty.", "Nothing to Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        await ImportAdvisorsToCrm(_currentMembers);
    }

    private async Task ImportSelectedToCrm()
    {
        var selected = _lvMembers.SelectedItems.Cast<ListViewItem>()
            .Where(i => i.Tag is int)
            .Select(i => _currentMembers.FirstOrDefault(a => a.Id == (int)i.Tag!))
            .Where(a => a != null).Cast<Advisor>().ToList();
        if (!selected.Any()) { await ImportListToCrm(); return; }
        await ImportAdvisorsToCrm(selected);
    }

    private async Task ImportAdvisorsToCrm(List<Advisor> advisors)
    {
        if (_wealthbox == null)
        {
            MessageBox.Show("Wealthbox is not configured. Please configure it in the main window (CRM > Wealthbox Settings).",
                "Wealthbox Not Configured", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var proceed = MessageBox.Show(
            $"Import {advisors.Count} advisor(s) to Wealthbox CRM?\n\nAlready-imported advisors will be updated.",
            "Confirm Import", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (proceed != DialogResult.Yes) return;

        _btnExportCrm.Enabled = false;
        int imported = 0, failed = 0;

        var progress = new Progress<string>(msg => _lblStatus.Text = msg);

        foreach (var advisor in advisors)
        {
            try
            {
                var crmId = await _wealthbox.ImportAdvisorAsync(advisor, progress);
                if (crmId != null)
                {
                    _advisorRepo.SetAdvisorImported(advisor.Id, crmId);
                    _onAdvisorImported?.Invoke(advisor.Id);
                    imported++;
                }
                else { failed++; }
            }
            catch { failed++; }
            await Task.Delay(200);
        }

        _btnExportCrm.Enabled = true;
        _lblStatus.Text = $"Import complete: {imported} imported, {failed} failed.";
        MessageBox.Show($"Import complete.\n\n{imported} advisor(s) imported.\n{failed} failed.",
            "Import Complete", MessageBoxButtons.OK, imported > 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (_lstLists.SelectedIndex >= 0 && _lstLists.SelectedIndex < _allLists.Count)
            LoadMembers(_allLists[_lstLists.SelectedIndex].Id);
    }

    private string GetSelectedListName()
    {
        if (_lstLists.SelectedIndex >= 0 && _lstLists.SelectedIndex < _allLists.Count)
            return _allLists[_lstLists.SelectedIndex].Name.Replace(" ", "_");
        return "list";
    }

    private void UpdateButtonStates()
    {
        bool hasList = _lstLists.SelectedIndex >= 0 && _allLists.Count > 0;
        bool hasSelection = _lvMembers.SelectedItems.Count > 0;
        _btnRenameList.Enabled = hasList;
        _btnDeleteList.Enabled = hasList;
        _btnRemoveMember.Enabled = hasSelection;
        _btnExportCsv.Enabled = _currentMembers.Count > 0;
        _btnExportAdvanced.Enabled = _currentMembers.Count > 0;
        _btnExportCrm.Enabled = _currentMembers.Count > 0 && _wealthbox != null;
    }

    private static Button MakeButton(string text, Color color, int width)
    {
        var btn = new Button
        {
            Text = text,
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Width = width,
            Height = 28,
            Font = new Font("Segoe UI", 9),
            Margin = new Padding(0, 0, 4, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }
}
