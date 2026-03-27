using AdvisorLeads.Models;

namespace AdvisorLeads.Controls;

public partial class FilterPanel : UserControl
{
    public event EventHandler? FiltersChanged;

    private TextBox _txtName = null!;
    private TextBox _txtCrd = null!;
    private ComboBox _cboState = null!;
    private TextBox _txtCity = null!;
    private TextBox _txtFirm = null!;
    private ComboBox _cboRecordType = null!;
    private NumericUpDown _numMinYears = null!;
    private NumericUpDown _numMaxYears = null!;
    private ComboBox _cboStatus = null!;
    private TextBox _txtLicense = null!;
    private ComboBox _cboSource = null!;
    private ComboBox _cboDisclosures = null!;
    private NumericUpDown _numMinDisclosures = null!;
    private ComboBox _cboCrm = null!;
    private CheckBox _chkShowExcluded = null!;
    private Button _btnApply = null!;
    private Button _btnClear = null!;
    private Label _lblBadge = null!;

    private bool _suppressAutoApply;

    public FilterPanel()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        this.BackColor = Color.FromArgb(245, 246, 250);
        this.Padding = new Padding(0);
        this.AutoScroll = true;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 26,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(6, 4, 6, 8),
            BackColor = Color.FromArgb(245, 246, 250)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        // ── Title + badge ──────────────────────────────────────────────────
        var titleFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 6),
            Margin = new Padding(0)
        };
        var lblTitle = new Label
        {
            Text = "Filter Advisors",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(50, 60, 140),
            AutoSize = true,
            Margin = new Padding(0, 0, 4, 0)
        };
        _lblBadge = new Label
        {
            Text = "0",
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(60, 100, 200),
            AutoSize = false,
            Size = new Size(22, 18),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(2, 2, 0, 0),
            Visible = false
        };
        titleFlow.Controls.Add(lblTitle);
        titleFlow.Controls.Add(_lblBadge);
        layout.SetColumnSpan(titleFlow, 2);
        layout.Controls.Add(titleFlow, 0, row++);

        // ── SEARCH ─────────────────────────────────────────────────────────
        AddSectionHeader(layout, "SEARCH", ref row);

        layout.Controls.Add(MakeLabel("Name:"), 0, row);
        _txtName = MakeTextBox();
        layout.Controls.Add(_txtName, 1, row++);

        layout.Controls.Add(MakeLabel("CRD #:"), 0, row);
        _txtCrd = MakeTextBox();
        layout.Controls.Add(_txtCrd, 1, row++);

        // ── LOCATION ───────────────────────────────────────────────────────
        AddSectionHeader(layout, "LOCATION", ref row);

        layout.Controls.Add(MakeLabel("State:"), 0, row);
        _cboState = MakeCombo();
        _cboState.Items.Add("(All)");
        _cboState.SelectedIndex = 0;
        layout.Controls.Add(_cboState, 1, row++);

        layout.Controls.Add(MakeLabel("City:"), 0, row);
        _txtCity = MakeTextBox();
        layout.Controls.Add(_txtCity, 1, row++);

        // ── EMPLOYMENT ─────────────────────────────────────────────────────
        AddSectionHeader(layout, "EMPLOYMENT", ref row);

        layout.Controls.Add(MakeLabel("Firm:"), 0, row);
        _txtFirm = MakeTextBox();
        layout.Controls.Add(_txtFirm, 1, row++);

        layout.Controls.Add(MakeLabel("Record Type:"), 0, row);
        _cboRecordType = MakeCombo();
        _cboRecordType.Items.AddRange(new[]
        {
            "(All)", "Investment Advisor Representative", "Registered Representative"
        });
        _cboRecordType.SelectedIndex = 0;
        layout.Controls.Add(_cboRecordType, 1, row++);

        layout.Controls.Add(MakeLabel("Min Exp (yr):"), 0, row);
        _numMinYears = MakeNumeric(0, 50, 0);
        layout.Controls.Add(_numMinYears, 1, row++);

        layout.Controls.Add(MakeLabel("Max Exp (yr):"), 0, row);
        _numMaxYears = MakeNumeric(0, 50, 50);
        layout.Controls.Add(_numMaxYears, 1, row++);

        // ── REGISTRATION ───────────────────────────────────────────────────
        AddSectionHeader(layout, "REGISTRATION", ref row);

        layout.Controls.Add(MakeLabel("Status:"), 0, row);
        _cboStatus = MakeCombo();
        _cboStatus.Items.AddRange(new[] { "(All)", "Active", "Inactive", "Barred", "Terminated", "Suspended" });
        _cboStatus.SelectedIndex = 0;
        layout.Controls.Add(_cboStatus, 1, row++);

        layout.Controls.Add(MakeLabel("License/Exam:"), 0, row);
        _txtLicense = MakeTextBox();
        layout.Controls.Add(_txtLicense, 1, row++);

        layout.Controls.Add(MakeLabel("Source:"), 0, row);
        _cboSource = MakeCombo();
        _cboSource.Items.AddRange(new[] { "(All)", "FINRA", "SEC", "Both" });
        _cboSource.SelectedIndex = 0;
        layout.Controls.Add(_cboSource, 1, row++);

        // ── DISCLOSURES ────────────────────────────────────────────────────
        AddSectionHeader(layout, "DISCLOSURES", ref row);

        layout.Controls.Add(MakeLabel("Disclosures:"), 0, row);
        _cboDisclosures = MakeCombo();
        _cboDisclosures.Items.AddRange(new[] { "(All)", "Has Disclosures", "No Disclosures" });
        _cboDisclosures.SelectedIndex = 0;
        layout.Controls.Add(_cboDisclosures, 1, row++);

        layout.Controls.Add(MakeLabel("Min Count:"), 0, row);
        _numMinDisclosures = MakeNumeric(0, 50, 0);
        layout.Controls.Add(_numMinDisclosures, 1, row++);

        // ── CRM & OTHER ────────────────────────────────────────────────────
        AddSectionHeader(layout, "CRM & OTHER", ref row);

        layout.Controls.Add(MakeLabel("CRM Import:"), 0, row);
        _cboCrm = MakeCombo();
        _cboCrm.Items.AddRange(new[] { "(All)", "Imported", "Not Imported" });
        _cboCrm.SelectedIndex = 0;
        layout.Controls.Add(_cboCrm, 1, row++);

        _chkShowExcluded = new CheckBox
        {
            Text = "Show Excluded",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8.5f),
            Margin = new Padding(0, 2, 4, 4)
        };
        layout.SetColumnSpan(_chkShowExcluded, 2);
        layout.Controls.Add(_chkShowExcluded, 0, row++);

        // ── QUICK FILTERS ──────────────────────────────────────────────────
        AddSectionHeader(layout, "QUICK FILTERS", ref row);

        var pillsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 4)
        };

        pillsFlow.Controls.Add(MakePill("Active IAR", () =>
        {
            _suppressAutoApply = true;
            ClearAllControls();
            SetComboByValue(_cboStatus, "Active");
            SetComboByValue(_cboRecordType, "Investment Advisor Representative");
            _suppressAutoApply = false;
            FireFiltersChanged();
        }));
        pillsFlow.Controls.Add(MakePill("Active RR", () =>
        {
            _suppressAutoApply = true;
            ClearAllControls();
            SetComboByValue(_cboStatus, "Active");
            SetComboByValue(_cboRecordType, "Registered Representative");
            _suppressAutoApply = false;
            FireFiltersChanged();
        }));
        pillsFlow.Controls.Add(MakePill("Has Disclosures", () =>
        {
            _suppressAutoApply = true;
            SetComboByValue(_cboDisclosures, "Has Disclosures");
            _suppressAutoApply = false;
            FireFiltersChanged();
        }));
        pillsFlow.Controls.Add(MakePill("Not in CRM", () =>
        {
            _suppressAutoApply = true;
            SetComboByValue(_cboCrm, "Not Imported");
            SetComboByValue(_cboStatus, "Active");
            _suppressAutoApply = false;
            FireFiltersChanged();
        }));

        layout.SetColumnSpan(pillsFlow, 2);
        layout.Controls.Add(pillsFlow, 0, row++);

        // ── Apply / Clear ──────────────────────────────────────────────────
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 4)
        };

        _btnApply = new Button
        {
            Text = "Apply",
            BackColor = Color.FromArgb(70, 100, 180),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Width = 72,
            Height = 28,
            Font = new Font("Segoe UI", 9)
        };
        _btnApply.FlatAppearance.BorderSize = 0;
        _btnApply.Click += (_, _) => FireFiltersChanged();

        _btnClear = new Button
        {
            Text = "Clear",
            BackColor = Color.FromArgb(200, 200, 200),
            ForeColor = Color.Black,
            FlatStyle = FlatStyle.Flat,
            Width = 60,
            Height = 28,
            Font = new Font("Segoe UI", 9)
        };
        _btnClear.FlatAppearance.BorderSize = 0;
        _btnClear.Click += OnClear;

        btnPanel.Controls.Add(_btnApply);
        btnPanel.Controls.Add(_btnClear);
        layout.SetColumnSpan(btnPanel, 2);
        layout.Controls.Add(btnPanel, 0, row);

        this.Controls.Add(layout);
        WireAutoApply();
    }

    private void WireAutoApply()
    {
        foreach (var cbo in new[] { _cboState, _cboRecordType, _cboStatus, _cboSource, _cboDisclosures, _cboCrm })
            cbo.SelectedIndexChanged += (_, _) => { if (!_suppressAutoApply) FireFiltersChanged(); };

        _chkShowExcluded.CheckedChanged += (_, _) => { if (!_suppressAutoApply) FireFiltersChanged(); };

        _numMinYears.ValueChanged += (_, _) => { if (!_suppressAutoApply) FireFiltersChanged(); };
        _numMaxYears.ValueChanged += (_, _) => { if (!_suppressAutoApply) FireFiltersChanged(); };
        _numMinDisclosures.ValueChanged += (_, _) => { if (!_suppressAutoApply) FireFiltersChanged(); };

        foreach (var txt in new[] { _txtName, _txtCrd, _txtCity, _txtFirm, _txtLicense })
            txt.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) FireFiltersChanged(); };
    }

    private void FireFiltersChanged()
    {
        UpdateFilterBadge();
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void AddSectionHeader(TableLayoutPanel layout, string title, ref int row)
    {
        var lbl = new Label
        {
            Text = "  " + title,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(60, 80, 160),
            BackColor = Color.FromArgb(225, 230, 248),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 8, 0, 3),
            Padding = new Padding(2, 2, 0, 2)
        };
        layout.SetColumnSpan(lbl, 2);
        layout.Controls.Add(lbl, 0, row++);
    }

    private static Button MakePill(string text, Action onClick)
    {
        var btn = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(228, 234, 255),
            ForeColor = Color.FromArgb(45, 65, 155),
            Font = new Font("Segoe UI", 7.5f),
            AutoSize = true,
            Padding = new Padding(5, 2, 5, 2),
            Margin = new Padding(0, 2, 4, 2),
            Height = 24
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(110, 140, 210);
        btn.FlatAppearance.BorderSize = 1;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static TextBox MakeTextBox() => new TextBox
    {
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 2, 4, 2)
    };

    private static ComboBox MakeCombo() => new ComboBox
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Margin = new Padding(0, 2, 4, 2)
    };

    private static NumericUpDown MakeNumeric(decimal min, decimal max, decimal value) => new NumericUpDown
    {
        Dock = DockStyle.Fill,
        Minimum = min,
        Maximum = max,
        Value = value,
        Margin = new Padding(0, 2, 4, 2)
    };

    private static Label MakeLabel(string text) => new Label
    {
        Text = text,
        TextAlign = ContentAlignment.MiddleRight,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 8.5f),
        Margin = new Padding(0, 2, 4, 2)
    };

    private int CountActiveFilters()
    {
        int count = 0;
        if (!string.IsNullOrWhiteSpace(_txtName.Text)) count++;
        if (!string.IsNullOrWhiteSpace(_txtCrd.Text)) count++;
        if (_cboState.SelectedIndex > 0) count++;
        if (!string.IsNullOrWhiteSpace(_txtCity.Text)) count++;
        if (!string.IsNullOrWhiteSpace(_txtFirm.Text)) count++;
        if (_cboRecordType.SelectedIndex > 0) count++;
        if (_numMinYears.Value > 0) count++;
        if (_numMaxYears.Value < 50) count++;
        if (_cboStatus.SelectedIndex > 0) count++;
        if (!string.IsNullOrWhiteSpace(_txtLicense.Text)) count++;
        if (_cboSource.SelectedIndex > 0) count++;
        if (_cboDisclosures.SelectedIndex > 0) count++;
        if (_numMinDisclosures.Value > 0) count++;
        if (_cboCrm.SelectedIndex > 0) count++;
        if (_chkShowExcluded.Checked) count++;
        return count;
    }

    private void UpdateFilterBadge()
    {
        var count = CountActiveFilters();
        if (count > 0)
        {
            _lblBadge.Text = count.ToString();
            _lblBadge.Visible = true;
        }
        else
        {
            _lblBadge.Visible = false;
        }
    }

    public SearchFilter GetFilter()
    {
        return new SearchFilter
        {
            NameQuery = string.IsNullOrWhiteSpace(_txtName.Text) ? null : _txtName.Text.Trim(),
            CrdNumber = string.IsNullOrWhiteSpace(_txtCrd.Text) ? null : _txtCrd.Text.Trim(),
            State = _cboState.SelectedIndex <= 0 ? null : _cboState.SelectedItem?.ToString(),
            City = string.IsNullOrWhiteSpace(_txtCity.Text) ? null : _txtCity.Text.Trim(),
            FirmName = string.IsNullOrWhiteSpace(_txtFirm.Text) ? null : _txtFirm.Text.Trim(),
            RecordType = _cboRecordType.SelectedIndex <= 0 ? null : _cboRecordType.SelectedItem?.ToString(),
            MinYearsExperience = _numMinYears.Value > 0 ? (int)_numMinYears.Value : null,
            MaxYearsExperience = _numMaxYears.Value < 50 ? (int)_numMaxYears.Value : null,
            RegistrationStatus = _cboStatus.SelectedIndex <= 0 ? null : _cboStatus.SelectedItem?.ToString(),
            LicenseType = string.IsNullOrWhiteSpace(_txtLicense.Text) ? null : _txtLicense.Text.Trim(),
            Source = _cboSource.SelectedIndex <= 0 ? null : _cboSource.SelectedItem?.ToString(),
            HasDisclosures = _cboDisclosures.SelectedIndex == 0 ? null
                           : _cboDisclosures.SelectedIndex == 1 ? true : false,
            MinDisclosureCount = _numMinDisclosures.Value > 0 ? (int)_numMinDisclosures.Value : null,
            IsImportedToCrm = _cboCrm.SelectedIndex == 0 ? null
                            : _cboCrm.SelectedIndex == 1 ? true : false,
            IncludeExcluded = _chkShowExcluded.Checked
        };
    }

    public void SetFilter(SearchFilter filter)
    {
        _suppressAutoApply = true;
        try
        {
            _txtName.Text = filter.NameQuery ?? "";
            _txtCrd.Text = filter.CrdNumber ?? "";
            SetComboByValue(_cboState, filter.State);
            _txtCity.Text = filter.City ?? "";
            _txtFirm.Text = filter.FirmName ?? "";
            SetComboByValue(_cboRecordType, filter.RecordType);
            _numMinYears.Value = Math.Max(0, Math.Min(50, filter.MinYearsExperience ?? 0));
            _numMaxYears.Value = Math.Max(0, Math.Min(50, filter.MaxYearsExperience ?? 50));
            SetComboByValue(_cboStatus, filter.RegistrationStatus);
            _txtLicense.Text = filter.LicenseType ?? "";
            SetComboByValue(_cboSource, filter.Source);
            _cboDisclosures.SelectedIndex = filter.HasDisclosures == null ? 0 : filter.HasDisclosures.Value ? 1 : 2;
            _numMinDisclosures.Value = Math.Max(0, Math.Min(50, filter.MinDisclosureCount ?? 0));
            _cboCrm.SelectedIndex = filter.IsImportedToCrm == null ? 0 : filter.IsImportedToCrm.Value ? 1 : 2;
            _chkShowExcluded.Checked = filter.IncludeExcluded;
        }
        finally
        {
            _suppressAutoApply = false;
        }
        UpdateFilterBadge();
    }

    public void PopulateStates(List<string> states)
    {
        var current = _cboState.SelectedItem?.ToString();
        _cboState.Items.Clear();
        _cboState.Items.Add("(All)");
        _cboState.Items.AddRange(states.ToArray());
        var idx = _cboState.Items.IndexOf(current ?? "(All)");
        _cboState.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private static void SetComboByValue(ComboBox cbo, string? value)
    {
        if (string.IsNullOrEmpty(value))
            cbo.SelectedIndex = 0;
        else
        {
            var idx = cbo.Items.IndexOf(value);
            cbo.SelectedIndex = idx >= 0 ? idx : 0;
        }
    }

    private void ClearAllControls()
    {
        _txtName.Clear();
        _txtCrd.Clear();
        _cboState.SelectedIndex = 0;
        _txtCity.Clear();
        _txtFirm.Clear();
        _cboRecordType.SelectedIndex = 0;
        _numMinYears.Value = 0;
        _numMaxYears.Value = 50;
        _cboStatus.SelectedIndex = 0;
        _txtLicense.Clear();
        _cboSource.SelectedIndex = 0;
        _cboDisclosures.SelectedIndex = 0;
        _numMinDisclosures.Value = 0;
        _cboCrm.SelectedIndex = 0;
        _chkShowExcluded.Checked = false;
    }

    private void OnClear(object? sender, EventArgs e)
    {
        _suppressAutoApply = true;
        ClearAllControls();
        _suppressAutoApply = false;
        FireFiltersChanged();
    }
}

