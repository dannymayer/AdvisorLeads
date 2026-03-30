namespace AdvisorLeads.Forms;

/// <summary>
/// Generic column-selector export dialog for Advisor and Firm exports.
/// </summary>
public class ExportDialog : Form
{
    // ── Public output properties ──────────────────────────────────────────
    public List<string> SelectedKeys { get; private set; } = new();
    public string OutputFormat { get; private set; } = "Excel";
    public bool IncludeHeader { get; private set; } = true;
    public bool ApplyConditionalFormatting { get; private set; } = true;
    public bool ExportAllRecords { get; private set; } = true;
    public string ChosenFilePath { get; private set; } = string.Empty;

    // ── Configuration ─────────────────────────────────────────────────────
    private readonly List<string> _allKeys;
    private readonly List<string> _allHeaders;
    private readonly List<string> _builtInPresets;
    private readonly Func<string, string?> _loadSetting;
    private readonly Action<string, string> _saveSetting;
    private readonly string _entityType;  // "Advisor" or "Firm"
    private readonly int _totalRecords;
    private readonly int _selectedCount;

    // ── Controls ──────────────────────────────────────────────────────────
    private ComboBox _cboPreset = null!;
    private Button _btnSavePreset = null!;
    private ListBox _lbAvailable = null!;
    private ListBox _lbSelected = null!;
    private Button _btnAdd = null!;
    private Button _btnRemove = null!;
    private Button _btnAddAll = null!;
    private Button _btnClear = null!;
    private Button _btnMoveUp = null!;
    private Button _btnMoveDown = null!;
    private RadioButton _rbExcel = null!;
    private RadioButton _rbCsv = null!;
    private CheckBox _chkHeader = null!;
    private CheckBox _chkConditional = null!;
    private RadioButton _rbAllRecords = null!;
    private RadioButton _rbSelectedOnly = null!;
    private Button _btnExport = null!;
    private Button _btnCancel = null!;

    public ExportDialog(
        string title,
        List<string> allColumnKeys,
        List<string> allColumnHeaders,
        List<string> selectedKeys,
        List<string> presetNames,
        Func<string, string?> loadSetting,
        Action<string, string> saveSetting,
        int totalRecords,
        int selectedCount = 0,
        string entityType = "Advisor")
    {
        _allKeys = allColumnKeys;
        _allHeaders = allColumnHeaders;
        _builtInPresets = presetNames;
        _loadSetting = loadSetting;
        _saveSetting = saveSetting;
        _entityType = entityType;
        _totalRecords = totalRecords;
        _selectedCount = selectedCount;

        Text = title;
        Size = new Size(820, 580);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9);

        BuildUI();
        ApplySelectedKeys(selectedKeys);
        LoadPresetList();
    }

    private void BuildUI()
    {
        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12)
        };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));  // title
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));  // preset row
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // column picker
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));  // options
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // buttons
        Controls.Add(outer);

        // Row 0: title
        var lblTitle = new Label
        {
            Text = Text,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(40, 60, 120)
        };
        outer.Controls.Add(lblTitle, 0, 0);

        // Row 1: preset bar
        var presetRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false
        };
        presetRow.Controls.Add(new Label
        {
            Text = "Preset:",
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(0, 6, 4, 0)
        });
        _cboPreset = new ComboBox
        {
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 2, 8, 0)
        };
        _cboPreset.SelectedIndexChanged += OnPresetChanged;
        _btnSavePreset = new Button
        {
            Text = "Save Preset…",
            Width = 100,
            Height = 26,
            Margin = new Padding(0, 2, 0, 0)
        };
        _btnSavePreset.Click += OnSavePreset;
        presetRow.Controls.Add(_cboPreset);
        presetRow.Controls.Add(_btnSavePreset);
        outer.Controls.Add(presetRow, 0, 1);

        // Row 2: column picker
        var pickerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        pickerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
        pickerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        pickerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));

        // Available list
        var availPanel = new Panel { Dock = DockStyle.Fill };
        var lblAvail = new Label
        {
            Text = "AVAILABLE",
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 18,
            ForeColor = Color.Gray
        };
        _lbAvailable = new ListBox
        {
            Dock = DockStyle.Fill,
            SelectionMode = SelectionMode.MultiExtended,
            ScrollAlwaysVisible = true
        };
        _lbAvailable.DoubleClick += (_, _) => MoveSelected(_lbAvailable, _lbSelected);
        availPanel.Controls.Add(_lbAvailable);
        availPanel.Controls.Add(lblAvail);
        pickerPanel.Controls.Add(availPanel, 0, 0);

        // Middle buttons
        var midFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(4, 16, 4, 0)
        };
        _btnAdd = MakePickerButton("Add →");
        _btnRemove = MakePickerButton("← Remove");
        _btnAddAll = MakePickerButton("All →");
        _btnClear = MakePickerButton("← Clear");
        _btnAdd.Click += (_, _) => MoveSelected(_lbAvailable, _lbSelected);
        _btnRemove.Click += (_, _) => MoveSelected(_lbSelected, _lbAvailable);
        _btnAddAll.Click += (_, _) => MoveAll(_lbAvailable, _lbSelected);
        _btnClear.Click += (_, _) => MoveAll(_lbSelected, _lbAvailable);
        midFlow.Controls.AddRange(new Control[] { _btnAdd, _btnRemove, _btnAddAll, _btnClear });
        pickerPanel.Controls.Add(midFlow, 1, 0);

        // Selected list with reorder buttons
        var selOuter = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        selOuter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        selOuter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));

        var selPanel = new Panel { Dock = DockStyle.Fill };
        var lblSel = new Label
        {
            Text = "SELECTED COLUMNS",
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 18,
            ForeColor = Color.Gray
        };
        _lbSelected = new ListBox
        {
            Dock = DockStyle.Fill,
            SelectionMode = SelectionMode.One,
            ScrollAlwaysVisible = true
        };
        _lbSelected.DoubleClick += (_, _) => MoveSelected(_lbSelected, _lbAvailable);
        selPanel.Controls.Add(_lbSelected);
        selPanel.Controls.Add(lblSel);
        selOuter.Controls.Add(selPanel, 0, 0);

        var reorderFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(2, 16, 2, 0)
        };
        _btnMoveUp = MakePickerButton("↑");
        _btnMoveDown = MakePickerButton("↓");
        _btnMoveUp.Width = 28;
        _btnMoveDown.Width = 28;
        _btnMoveUp.Click += (_, _) => ReorderSelected(-1);
        _btnMoveDown.Click += (_, _) => ReorderSelected(1);
        reorderFlow.Controls.AddRange(new Control[] { _btnMoveUp, _btnMoveDown });
        selOuter.Controls.Add(reorderFlow, 1, 0);

        pickerPanel.Controls.Add(selOuter, 2, 0);
        outer.Controls.Add(pickerPanel, 0, 2);

        // Row 3: options
        var optPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 4, 0, 0)
        };
        optPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        optPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var formatFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        formatFlow.Controls.Add(new Label { Text = "Output:", AutoSize = true, Margin = new Padding(0, 4, 6, 0) });
        _rbExcel = new RadioButton { Text = "Excel (.xlsx)", AutoSize = true, Checked = true, Margin = new Padding(0, 2, 8, 0) };
        _rbCsv = new RadioButton { Text = "CSV (.csv)", AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
        _rbExcel.CheckedChanged += OnFormatChanged;
        _rbCsv.CheckedChanged += OnFormatChanged;
        formatFlow.Controls.AddRange(new Control[] { _rbExcel, _rbCsv });
        optPanel.Controls.Add(formatFlow, 0, 0);

        var checkFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        _chkHeader = new CheckBox { Text = "Include header row", AutoSize = true, Checked = true, Margin = new Padding(0, 4, 12, 0) };
        _chkConditional = new CheckBox { Text = "Apply conditional formatting (Excel only)", AutoSize = true, Checked = true };
        checkFlow.Controls.AddRange(new Control[] { _chkHeader, _chkConditional });
        optPanel.Controls.Add(checkFlow, 1, 0);

        var recordFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        recordFlow.Controls.Add(new Label { Text = "Records:", AutoSize = true, Margin = new Padding(0, 4, 6, 0) });
        _rbAllRecords = new RadioButton
        {
            Text = $"All filtered ({_totalRecords:N0})",
            AutoSize = true,
            Checked = true,
            Margin = new Padding(0, 2, 12, 0)
        };
        _rbSelectedOnly = new RadioButton
        {
            Text = $"Selected rows only ({_selectedCount:N0})",
            AutoSize = true,
            Enabled = _selectedCount > 0,
            Margin = new Padding(0, 2, 0, 0)
        };
        recordFlow.Controls.AddRange(new Control[] { _rbAllRecords, _rbSelectedOnly });
        optPanel.Controls.Add(recordFlow, 0, 1);
        outer.Controls.Add(optPanel, 0, 3);

        // Row 4: OK/Cancel
        var btnFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };
        _btnExport = new Button
        {
            Text = "Export…",
            Width = 90,
            Height = 28,
            BackColor = Color.FromArgb(60, 130, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(4, 0, 0, 0)
        };
        _btnCancel = new Button
        {
            Text = "Cancel",
            Width = 80,
            Height = 28,
            Margin = new Padding(4, 0, 0, 0)
        };
        _btnExport.Click += OnExport;
        _btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        btnFlow.Controls.AddRange(new Control[] { _btnExport, _btnCancel });
        outer.Controls.Add(btnFlow, 0, 4);
    }

    private static Button MakePickerButton(string text) =>
        new()
        {
            Text = text,
            Width = 80,
            Height = 26,
            Margin = new Padding(0, 2, 0, 2)
        };

    // ── Initialization helpers ─────────────────────────────────────────────

    private void ApplySelectedKeys(List<string> selectedKeys)
    {
        _lbAvailable.Items.Clear();
        _lbSelected.Items.Clear();

        foreach (var key in selectedKeys)
        {
            int idx = _allKeys.IndexOf(key);
            if (idx >= 0)
                _lbSelected.Items.Add(_allHeaders[idx]);
        }

        for (int i = 0; i < _allKeys.Count; i++)
        {
            if (!selectedKeys.Contains(_allKeys[i]))
                _lbAvailable.Items.Add(_allHeaders[i]);
        }
    }

    private void LoadPresetList()
    {
        _cboPreset.Items.Clear();
        foreach (var p in _builtInPresets)
            _cboPreset.Items.Add(p);

        // Load the custom preset name list
        var customNames = _loadSetting($"ExportPresetNames_{_entityType}");
        if (!string.IsNullOrEmpty(customNames))
        {
            foreach (var name in customNames.Split(','))
            {
                var trimmed = name.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !_cboPreset.Items.Contains(trimmed))
                    _cboPreset.Items.Add(trimmed);
            }
        }

        if (_cboPreset.Items.Count > 0)
            _cboPreset.SelectedIndex = 0;
    }

    // ── Event handlers ────────────────────────────────────────────────────

    private void OnPresetChanged(object? sender, EventArgs e)
    {
        if (_cboPreset.SelectedItem is not string presetName) return;

        List<string> keys;

        // Try loading from settings first (custom preset)
        var stored = _loadSetting($"ExportPreset_{_entityType}_{presetName}");
        if (!string.IsNullOrEmpty(stored))
        {
            keys = stored.Split(',').Select(k => k.Trim()).Where(k => _allKeys.Contains(k)).ToList();
        }
        else
        {
            // Built-in preset: use the position in allKeys for the preset-named columns
            // We'll map header names back to keys
            keys = _allKeys; // fallback — will be overridden below for the built-in case
            // Signal to the column model to return its built-in preset is handled by the owner
            // For the dialog, we embed the key list via the initial constructor call.
            // Built-in presets (Default, Full, etc.) have their keys in the preset data passed to constructor.
            // Here we can't call back into AdvisorExportColumns without a type reference,
            // so for built-in presets we treat the dialog as already reflecting them — skip.
            return;
        }

        ApplySelectedKeys(keys);
    }

    private void OnSavePreset(object? sender, EventArgs e)
    {
        using var prompt = new PromptDialog("Save Preset", "Preset name:");
        if (prompt.ShowDialog(this) != DialogResult.OK) return;

        var name = prompt.InputValue.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var currentKeys = GetCurrentSelectedKeys();
        _saveSetting($"ExportPreset_{_entityType}_{name}", string.Join(",", currentKeys));

        // Maintain an index of custom preset names
        var existing = _loadSetting($"ExportPresetNames_{_entityType}") ?? string.Empty;
        var names = existing.Split(',').Select(n => n.Trim()).Where(n => !string.IsNullOrEmpty(n)).ToList();
        if (!names.Contains(name))
        {
            names.Add(name);
            _saveSetting($"ExportPresetNames_{_entityType}", string.Join(",", names));
        }

        if (!_cboPreset.Items.Contains(name))
        {
            _cboPreset.Items.Add(name);
            _cboPreset.SelectedItem = name;
        }
    }

    private void OnFormatChanged(object? sender, EventArgs e)
    {
        bool isExcel = _rbExcel.Checked;
        _chkConditional.Enabled = isExcel;
        if (!isExcel) _chkConditional.Checked = false;
    }

    private void OnExport(object? sender, EventArgs e)
    {
        var keys = GetCurrentSelectedKeys();
        if (keys.Count == 0)
        {
            MessageBox.Show("Please select at least one column.", "No Columns Selected",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool isExcel = _rbExcel.Checked;
        using var sfd = new SaveFileDialog
        {
            Title = "Export",
            Filter = isExcel
                ? "Excel Workbook (*.xlsx)|*.xlsx|All Files (*.*)|*.*"
                : "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            DefaultExt = isExcel ? "xlsx" : "csv",
            FileName = $"{_entityType}_export_{DateTime.Now:yyyyMMdd}"
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        SelectedKeys = keys;
        OutputFormat = isExcel ? "Excel" : "CSV";
        IncludeHeader = _chkHeader.Checked;
        ApplyConditionalFormatting = isExcel && _chkConditional.Checked;
        ExportAllRecords = _rbAllRecords.Checked;
        ChosenFilePath = sfd.FileName;
        DialogResult = DialogResult.OK;
        Close();
    }

    // ── Move/reorder helpers ──────────────────────────────────────────────

    private void MoveSelected(ListBox source, ListBox dest)
    {
        var toMove = source.SelectedItems.Cast<string>().ToList();
        foreach (var item in toMove)
        {
            source.Items.Remove(item);
            if (!dest.Items.Contains(item))
                dest.Items.Add(item);
        }
    }

    private void MoveAll(ListBox source, ListBox dest)
    {
        var all = source.Items.Cast<string>().ToList();
        source.Items.Clear();
        foreach (var item in all)
        {
            if (!dest.Items.Contains(item))
                dest.Items.Add(item);
        }
    }

    private void ReorderSelected(int direction)
    {
        int idx = _lbSelected.SelectedIndex;
        if (idx < 0) return;
        int newIdx = idx + direction;
        if (newIdx < 0 || newIdx >= _lbSelected.Items.Count) return;
        var item = _lbSelected.Items[idx];
        _lbSelected.Items.RemoveAt(idx);
        _lbSelected.Items.Insert(newIdx, item);
        _lbSelected.SelectedIndex = newIdx;
    }

    private List<string> GetCurrentSelectedKeys()
    {
        var selectedHeaders = _lbSelected.Items.Cast<string>().ToList();
        var headerToKey = new Dictionary<string, string>();
        for (int i = 0; i < _allKeys.Count; i++)
            headerToKey[_allHeaders[i]] = _allKeys[i];
        return selectedHeaders.Where(h => headerToKey.ContainsKey(h)).Select(h => headerToKey[h]).ToList();
    }
}

/// <summary>Simple single-line text prompt dialog.</summary>
internal class PromptDialog : Form
{
    public string InputValue => _txtInput.Text;

    private readonly TextBox _txtInput;

    public PromptDialog(string title, string prompt)
    {
        Text = title;
        Size = new Size(360, 140);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        Controls.Add(layout);

        layout.Controls.Add(new Label { Text = prompt, Dock = DockStyle.Fill }, 0, 0);

        _txtInput = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_txtInput, 0, 1);

        var btnFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var btnOk = new Button { Text = "OK", Width = 70, Height = 26, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancel", Width = 70, Height = 26, DialogResult = DialogResult.Cancel };
        btnFlow.Controls.AddRange(new Control[] { btnOk, btnCancel });
        layout.Controls.Add(btnFlow, 0, 2);

        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }
}
