using AdvisorLeads.Models;

namespace AdvisorLeads.Controls;

public partial class FilterPanel : UserControl
{
    public event EventHandler? FiltersChanged;

    private TextBox _txtName = null!;
    private ComboBox _cboState = null!;
    private TextBox _txtFirm = null!;
    private ComboBox _cboStatus = null!;
    private TextBox _txtLicense = null!;
    private ComboBox _cboDisclosures = null!;
    private CheckBox _chkShowExcluded = null!;
    private ComboBox _cboCrm = null!;
    private ComboBox _cboSource = null!;
    private NumericUpDown _numMinYears = null!;
    private NumericUpDown _numMaxYears = null!;
    private Button _btnApply = null!;
    private Button _btnClear = null!;

    public FilterPanel()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        this.BackColor = Color.FromArgb(240, 240, 248);
        this.Padding = new Padding(8);
        this.AutoScroll = true;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 13,
            Padding = new Padding(4),
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        // Title
        var lblTitle = new Label
        {
            Text = "Filters",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(50, 50, 120),
            Padding = new Padding(0, 4, 0, 8)
        };
        layout.SetColumnSpan(lblTitle, 2);
        layout.Controls.Add(lblTitle, 0, row++);

        // Name
        layout.Controls.Add(MakeLabel("Name:"), 0, row);
        _txtName = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_txtName, 1, row++);

        // State
        layout.Controls.Add(MakeLabel("State:"), 0, row);
        _cboState = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboState.Items.Add("(All)");
        layout.Controls.Add(_cboState, 1, row++);

        // Firm
        layout.Controls.Add(MakeLabel("Firm:"), 0, row);
        _txtFirm = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_txtFirm, 1, row++);

        // Status
        layout.Controls.Add(MakeLabel("Status:"), 0, row);
        _cboStatus = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboStatus.Items.AddRange(new[] { "(All)", "Approved", "Active", "Inactive", "Barred", "Terminated" });
        _cboStatus.SelectedIndex = 0;
        layout.Controls.Add(_cboStatus, 1, row++);

        // License
        layout.Controls.Add(MakeLabel("License:"), 0, row);
        _txtLicense = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_txtLicense, 1, row++);

        // Disclosures
        layout.Controls.Add(MakeLabel("Disclosures:"), 0, row);
        _cboDisclosures = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboDisclosures.Items.AddRange(new[] { "(All)", "Has Disclosures", "No Disclosures" });
        _cboDisclosures.SelectedIndex = 0;
        layout.Controls.Add(_cboDisclosures, 1, row++);

        // Source
        layout.Controls.Add(MakeLabel("Source:"), 0, row);
        _cboSource = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboSource.Items.AddRange(new[] { "(All)", "FINRA", "SEC", "FINRA,SEC" });
        _cboSource.SelectedIndex = 0;
        layout.Controls.Add(_cboSource, 1, row++);

        // CRM
        layout.Controls.Add(MakeLabel("CRM Import:"), 0, row);
        _cboCrm = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboCrm.Items.AddRange(new[] { "(All)", "Imported", "Not Imported" });
        _cboCrm.SelectedIndex = 0;
        layout.Controls.Add(_cboCrm, 1, row++);

        // Min Years
        layout.Controls.Add(MakeLabel("Min Years:"), 0, row);
        _numMinYears = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 0, Maximum = 50, Value = 0 };
        layout.Controls.Add(_numMinYears, 1, row++);

        // Max Years
        layout.Controls.Add(MakeLabel("Max Years:"), 0, row);
        _numMaxYears = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 0, Maximum = 50, Value = 50 };
        layout.Controls.Add(_numMaxYears, 1, row++);

        // Show excluded
        _chkShowExcluded = new CheckBox
        {
            Text = "Show excluded",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8.5f)
        };
        layout.SetColumnSpan(_chkShowExcluded, 2);
        layout.Controls.Add(_chkShowExcluded, 0, row++);

        // Buttons
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 0)
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
        _btnApply.Click += (_, _) => FiltersChanged?.Invoke(this, EventArgs.Empty);

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
        layout.Controls.Add(btnPanel, 0, row++);

        this.Controls.Add(layout);

        // Allow Enter key to apply filters
        _txtName.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) FiltersChanged?.Invoke(this, EventArgs.Empty); };
        _txtFirm.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) FiltersChanged?.Invoke(this, EventArgs.Empty); };
        _txtLicense.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) FiltersChanged?.Invoke(this, EventArgs.Empty); };
    }

    private static Label MakeLabel(string text) => new Label
    {
        Text = text,
        TextAlign = ContentAlignment.MiddleRight,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 8.5f)
    };

    public SearchFilter GetFilter()
    {
        return new SearchFilter
        {
            NameQuery = string.IsNullOrWhiteSpace(_txtName.Text) ? null : _txtName.Text.Trim(),
            State = _cboState.SelectedIndex <= 0 ? null : _cboState.SelectedItem?.ToString(),
            FirmName = string.IsNullOrWhiteSpace(_txtFirm.Text) ? null : _txtFirm.Text.Trim(),
            RegistrationStatus = _cboStatus.SelectedIndex <= 0 ? null : _cboStatus.SelectedItem?.ToString(),
            LicenseType = string.IsNullOrWhiteSpace(_txtLicense.Text) ? null : _txtLicense.Text.Trim(),
            HasDisclosures = _cboDisclosures.SelectedIndex == 0 ? null
                           : _cboDisclosures.SelectedIndex == 1 ? true : false,
            IsImportedToCrm = _cboCrm.SelectedIndex == 0 ? null
                            : _cboCrm.SelectedIndex == 1 ? true : false,
            Source = _cboSource.SelectedIndex <= 0 ? null : _cboSource.SelectedItem?.ToString(),
            IncludeExcluded = _chkShowExcluded.Checked,
            MinYearsExperience = _numMinYears.Value > 0 ? (int)_numMinYears.Value : null,
            MaxYearsExperience = _numMaxYears.Value < 50 ? (int)_numMaxYears.Value : null
        };
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

    private void OnClear(object? sender, EventArgs e)
    {
        _txtName.Clear();
        _cboState.SelectedIndex = 0;
        _txtFirm.Clear();
        _cboStatus.SelectedIndex = 0;
        _txtLicense.Clear();
        _cboDisclosures.SelectedIndex = 0;
        _cboSource.SelectedIndex = 0;
        _cboCrm.SelectedIndex = 0;
        _chkShowExcluded.Checked = false;
        _numMinYears.Value = 0;
        _numMaxYears.Value = 50;
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }
}
