using AdvisorLeads.Models;

namespace AdvisorLeads.Controls;

public class FirmFilterPanel : UserControl
{
    public event EventHandler? FiltersChanged;

    private TextBox _txtName = null!;
    private ComboBox _cboState = null!;
    private ComboBox _cboRecordType = null!;
    private ComboBox _cboStatus = null!;
    private NumericUpDown _numMinAdvisors = null!;
    private CheckBox _chkBrokerProtocol = null!;
    private ComboBox _cboMinAum = null!;
    private ComboBox _cboCompensation = null!;
    private CheckBox _chkHasCustody = null!;
    private Button _btnApply = null!;
    private Button _btnClear = null!;

    public FirmFilterPanel()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        this.MinimumSize = new Size(0, 44);
        this.BackColor = Color.FromArgb(240, 242, 250);
        this.Padding = new Padding(4, 6, 4, 4);
        this.Dock = DockStyle.Top;
        this.AutoSize = true;
        this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0)
        };

        flow.Controls.Add(MakeLabel("Name:"));
        _txtName = new TextBox { Width = 140, Height = 24, Margin = new Padding(0, 2, 8, 0) };
        flow.Controls.Add(_txtName);

        flow.Controls.Add(MakeLabel("State:"));
        _cboState = new ComboBox { Width = 80, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 8, 0) };
        _cboState.Items.Add("(All)");
        _cboState.SelectedIndex = 0;
        flow.Controls.Add(_cboState);

        flow.Controls.Add(MakeLabel("Type:"));
        _cboRecordType = new ComboBox { Width = 145, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 8, 0) };
        _cboRecordType.Items.AddRange(new[] { "(All)", "Investment Adviser", "Broker-Dealer" });
        _cboRecordType.SelectedIndex = 0;
        flow.Controls.Add(_cboRecordType);

        flow.Controls.Add(MakeLabel("Status:"));
        _cboStatus = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 8, 0) };
        _cboStatus.Items.AddRange(new[] { "(All)", "Active", "Inactive", "Terminated" });
        _cboStatus.SelectedIndex = 0;
        flow.Controls.Add(_cboStatus);

        flow.Controls.Add(MakeLabel("Min Adv:"));
        _numMinAdvisors = new NumericUpDown { Width = 60, Minimum = 0, Maximum = 10000, Value = 0, Margin = new Padding(0, 2, 8, 0) };
        flow.Controls.Add(_numMinAdvisors);

        flow.Controls.Add(MakeLabel("Min AUM:"));
        _cboMinAum = new ComboBox { Width = 80, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 8, 0) };
        _cboMinAum.Items.AddRange(new[] { "Any AUM", "$1M+", "$10M+", "$100M+", "$500M+", "$1B+" });
        _cboMinAum.SelectedIndex = 0;
        flow.Controls.Add(_cboMinAum);

        _chkBrokerProtocol = new CheckBox
        {
            Text = "Broker Protocol",
            AutoSize = false,
            Width = 110,
            Height = 24,
            Margin = new Padding(4, 3, 8, 0),
            Font = new Font("Segoe UI", 8.5f)
        };
        flow.Controls.Add(_chkBrokerProtocol);

        flow.Controls.Add(MakeLabel("Comp:"));
        _cboCompensation = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 8, 0) };
        _cboCompensation.Items.AddRange(new[] { "(All)", "Fee-Only", "Commission", "Both" });
        _cboCompensation.SelectedIndex = 0;
        flow.Controls.Add(_cboCompensation);

        _chkHasCustody = new CheckBox
        {
            Text = "Has Custody",
            AutoSize = false,
            Width = 90,
            Height = 24,
            Margin = new Padding(4, 3, 8, 0),
            Font = new Font("Segoe UI", 8.5f)
        };
        flow.Controls.Add(_chkHasCustody);

        _btnApply = new Button
        {
            Text = "Apply",
            Width = 60,
            Height = 26,
            Margin = new Padding(4, 1, 4, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(70, 100, 180),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9)
        };
        _btnApply.FlatAppearance.BorderSize = 0;
        _btnApply.Click += (_, _) => FiltersChanged?.Invoke(this, EventArgs.Empty);
        flow.Controls.Add(_btnApply);

        _btnClear = new Button
        {
            Text = "Clear",
            Width = 55,
            Height = 26,
            Margin = new Padding(0, 1, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(200, 200, 200),
            Font = new Font("Segoe UI", 9)
        };
        _btnClear.FlatAppearance.BorderSize = 0;
        _btnClear.Click += OnClear;
        flow.Controls.Add(_btnClear);

        this.Controls.Add(flow);

        // Auto-apply for dropdowns
        _cboState.SelectedIndexChanged += (_, _) => FiltersChanged?.Invoke(this, EventArgs.Empty);
        _cboRecordType.SelectedIndexChanged += (_, _) => FiltersChanged?.Invoke(this, EventArgs.Empty);
        _cboStatus.SelectedIndexChanged += (_, _) => FiltersChanged?.Invoke(this, EventArgs.Empty);
        _txtName.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) FiltersChanged?.Invoke(this, EventArgs.Empty); };
        _cboMinAum.SelectedIndexChanged += (_, _) => FiltersChanged?.Invoke(this, EventArgs.Empty);
        _cboCompensation.SelectedIndexChanged += (_, _) => FiltersChanged?.Invoke(this, EventArgs.Empty);
        _chkBrokerProtocol.CheckedChanged += (_, _) => FiltersChanged?.Invoke(this, EventArgs.Empty);
        _chkHasCustody.CheckedChanged += (_, _) => FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    private static Label MakeLabel(string text) => new Label
    {
        Text = text,
        AutoSize = false,
        Width = 52,
        Height = 24,
        TextAlign = ContentAlignment.MiddleRight,
        Font = new Font("Segoe UI", 8.5f),
        Margin = new Padding(4, 2, 2, 0)
    };

    public FirmSearchFilter GetFilter()
    {
        return new FirmSearchFilter
        {
            NameQuery = string.IsNullOrWhiteSpace(_txtName.Text) ? null : _txtName.Text.Trim(),
            State = _cboState.SelectedIndex <= 0 ? null : _cboState.SelectedItem?.ToString(),
            RecordType = _cboRecordType.SelectedIndex <= 0 ? null : _cboRecordType.SelectedItem?.ToString(),
            RegistrationStatus = _cboStatus.SelectedIndex <= 0 ? null : _cboStatus.SelectedItem?.ToString(),
            MinAdvisors = _numMinAdvisors.Value > 0 ? (int)_numMinAdvisors.Value : null,
            BrokerProtocolOnly = _chkBrokerProtocol.Checked,
            HasCustody = _chkHasCustody.Checked ? true : null,
            CompensationType = _cboCompensation.SelectedIndex <= 0 ? null : _cboCompensation.SelectedItem?.ToString(),
            MinRegulatoryAum = _cboMinAum.SelectedIndex switch
            {
                1 => 1_000_000m,
                2 => 10_000_000m,
                3 => 100_000_000m,
                4 => 500_000_000m,
                5 => 1_000_000_000m,
                _ => null
            }
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

    public void Clear() => OnClear(this, EventArgs.Empty);

    private void OnClear(object? sender, EventArgs e)
    {
        _txtName.Clear();
        _cboState.SelectedIndex = 0;
        _cboRecordType.SelectedIndex = 0;
        _cboStatus.SelectedIndex = 0;
        _numMinAdvisors.Value = 0;
        _cboMinAum.SelectedIndex = 0;
        _cboCompensation.SelectedIndex = 0;
        _chkBrokerProtocol.Checked = false;
        _chkHasCustody.Checked = false;
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }
}
