using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Forms;

/// <summary>
/// Dialog for creating or editing a MarketWatchRule.
/// </summary>
public class MarketWatchRuleDialog : Form
{
    private readonly AlertRepository _alertRepo;
    private readonly MarketWatchRule _rule;

    private TextBox _txtName = null!;
    private ComboBox _cmbState = null!;
    private ComboBox _cmbRecordType = null!;
    private TextBox _txtLicense = null!;
    private NumericUpDown _nudMinExp = null!;
    private CheckBox _chkEnabled = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;

    private static readonly string[] StateCodes =
    {
        "AL","AK","AZ","AR","CA","CO","CT","DE","FL","GA",
        "HI","ID","IL","IN","IA","KS","KY","LA","ME","MD",
        "MA","MI","MN","MS","MO","MT","NE","NV","NH","NJ",
        "NM","NY","NC","ND","OH","OK","OR","PA","RI","SC",
        "SD","TN","TX","UT","VT","VA","WA","WV","WI","WY",
        "DC","PR","GU","VI"
    };

    public MarketWatchRuleDialog(AlertRepository alertRepo, MarketWatchRule? existingRule)
    {
        _alertRepo = alertRepo;
        _rule = existingRule ?? new MarketWatchRule { IsActive = true };
        BuildUI();
        PopulateFields();
    }

    private void BuildUI()
    {
        this.Text = _rule.Id == 0 ? "Add Market Watch Rule" : "Edit Market Watch Rule";
        this.Size = new Size(480, 380);
        this.MinimumSize = new Size(480, 380);
        this.MaximumSize = new Size(480, 380);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterParent;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(16),
            AutoSize = false
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        // Rule name
        layout.Controls.Add(MakeLabel("Rule Name:"), 0, row);
        _txtName = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_txtName, 1, row);
        row++;

        // State
        layout.Controls.Add(MakeLabel("State (blank = Any):"), 0, row);
        _cmbState = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbState.Items.Add("(Any)");
        foreach (var s in StateCodes) _cmbState.Items.Add(s);
        _cmbState.SelectedIndex = 0;
        layout.Controls.Add(_cmbState, 1, row);
        row++;

        // Record type
        layout.Controls.Add(MakeLabel("Record Type:"), 0, row);
        _cmbRecordType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbRecordType.Items.Add("(Any)");
        _cmbRecordType.Items.Add("Investment Advisor Representative");
        _cmbRecordType.Items.Add("Registered Representative");
        _cmbRecordType.SelectedIndex = 0;
        layout.Controls.Add(_cmbRecordType, 1, row);
        row++;

        // License
        layout.Controls.Add(MakeLabel("License Contains:"), 0, row);
        _txtLicense = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_txtLicense, 1, row);
        row++;

        // Min years experience
        layout.Controls.Add(MakeLabel("Min Years Experience:"), 0, row);
        _nudMinExp = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 40,
            Value = 0,
            DecimalPlaces = 0
        };
        layout.Controls.Add(_nudMinExp, 1, row);
        row++;

        // Enabled
        layout.Controls.Add(MakeLabel("Enabled:"), 0, row);
        _chkEnabled = new CheckBox { AutoSize = true, Checked = true };
        layout.Controls.Add(_chkEnabled, 1, row);
        row++;

        // Spacer
        layout.Controls.Add(new Label(), 0, row);
        row++;

        // Buttons
        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        _btnCancel = new Button
        {
            Text = "Cancel",
            Width = 80,
            Height = 30,
            DialogResult = DialogResult.Cancel
        };
        _btnSave = new Button
        {
            Text = "Save",
            Width = 80,
            Height = 30,
            BackColor = Color.FromArgb(0, 100, 160),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += OnSave;

        btnRow.Controls.Add(_btnCancel);
        btnRow.Controls.Add(_btnSave);
        layout.SetColumnSpan(btnRow, 2);
        layout.Controls.Add(btnRow, 0, row);

        this.Controls.Add(layout);
        this.AcceptButton = _btnSave;
        this.CancelButton = _btnCancel;
    }

    private void PopulateFields()
    {
        if (_rule.Id == 0) return;

        _txtName.Text = _rule.RuleName;

        if (!string.IsNullOrEmpty(_rule.State))
        {
            int idx = _cmbState.Items.IndexOf(_rule.State);
            _cmbState.SelectedIndex = idx >= 0 ? idx : 0;
        }

        if (!string.IsNullOrEmpty(_rule.RecordType))
        {
            int idx = _cmbRecordType.Items.IndexOf(_rule.RecordType);
            _cmbRecordType.SelectedIndex = idx >= 0 ? idx : 0;
        }

        _txtLicense.Text = _rule.LicenseContains ?? "";
        _nudMinExp.Value = _rule.MinYearsExperience.HasValue
            ? Math.Min(40, Math.Max(0, _rule.MinYearsExperience.Value))
            : 0;
        _chkEnabled.Checked = _rule.IsActive;
    }

    private void OnSave(object? sender, EventArgs e)
    {
        string name = _txtName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Rule Name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return;
        }

        _rule.RuleName = name;
        _rule.State = _cmbState.SelectedIndex == 0 ? null : _cmbState.SelectedItem?.ToString();
        _rule.RecordType = _cmbRecordType.SelectedIndex == 0 ? null : _cmbRecordType.SelectedItem?.ToString();
        _rule.LicenseContains = string.IsNullOrWhiteSpace(_txtLicense.Text) ? null : _txtLicense.Text.Trim();
        _rule.MinYearsExperience = _nudMinExp.Value > 0 ? (int)_nudMinExp.Value : (int?)null;
        _rule.IsActive = _chkEnabled.Checked;

        try
        {
            _alertRepo.UpsertMarketWatchRule(_rule);
            this.DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save rule: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Label MakeLabel(string text) => new Label
    {
        Text = text,
        TextAlign = ContentAlignment.MiddleRight,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 9, FontStyle.Bold),
        ForeColor = Color.FromArgb(60, 60, 80),
        Padding = new Padding(0, 4, 8, 4)
    };
}
