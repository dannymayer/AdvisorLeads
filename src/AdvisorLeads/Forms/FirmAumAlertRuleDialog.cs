using AdvisorLeads.Data;
using AdvisorLeads.Models;

namespace AdvisorLeads.Forms;

/// <summary>
/// Dialog for creating or editing a FirmAumAlertRule.
/// </summary>
public class FirmAumAlertRuleDialog : Form
{
    private readonly AlertRepository _alertRepo;
    private readonly FirmAumAlertRule _rule;
    private readonly Func<string, string?>? _firmNameLookup;

    private TextBox _txtCrd = null!;
    private Label _lblFirmName = null!;
    private ComboBox _cmbDirection = null!;
    private TextBox _txtThresholdAum = null!;
    private CheckBox _chkActive = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;

    public FirmAumAlertRuleDialog(
        AlertRepository alertRepo,
        FirmAumAlertRule? existingRule,
        Func<string, string?>? firmNameLookup = null)
    {
        _alertRepo = alertRepo;
        _rule = existingRule ?? new FirmAumAlertRule { IsActive = true };
        _firmNameLookup = firmNameLookup;
        BuildUI();
        PopulateFields();
    }

    private void BuildUI()
    {
        this.Text = _rule.Id == 0 ? "Add AUM Alert Rule" : "Edit AUM Alert Rule";
        this.Size = new Size(480, 300);
        this.MinimumSize = new Size(480, 300);
        this.MaximumSize = new Size(480, 300);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterParent;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(16),
            AutoSize = false
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

        int row = 0;

        // Firm CRD
        layout.Controls.Add(MakeLabel("Firm CRD:"), 0, row);
        _txtCrd = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_txtCrd, 1, row);
        var btnLookup = new Button
        {
            Text = "Lookup",
            Dock = DockStyle.Fill,
            Height = 24,
            FlatStyle = FlatStyle.Flat
        };
        btnLookup.Click += OnLookupFirm;
        layout.Controls.Add(btnLookup, 2, row);
        row++;

        // Firm name (read-only label)
        layout.Controls.Add(MakeLabel("Firm Name:"), 0, row);
        _lblFirmName = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(60, 120, 60),
            Font = new Font("Segoe UI", 9, FontStyle.Italic),
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.SetColumnSpan(_lblFirmName, 2);
        layout.Controls.Add(_lblFirmName, 1, row);
        row++;

        // Direction
        layout.Controls.Add(MakeLabel("Direction:"), 0, row);
        _cmbDirection = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbDirection.Items.AddRange(new object[] { "CrossAbove", "CrossBelow" });
        _cmbDirection.SelectedIndex = 0;
        layout.SetColumnSpan(_cmbDirection, 2);
        layout.Controls.Add(_cmbDirection, 1, row);
        row++;

        // Threshold AUM
        layout.Controls.Add(MakeLabel("Threshold AUM ($):"), 0, row);
        _txtThresholdAum = new TextBox { Dock = DockStyle.Fill };
        layout.SetColumnSpan(_txtThresholdAum, 2);
        layout.Controls.Add(_txtThresholdAum, 1, row);
        row++;

        // Active
        layout.Controls.Add(MakeLabel("Active:"), 0, row);
        _chkActive = new CheckBox { AutoSize = true, Checked = true };
        layout.SetColumnSpan(_chkActive, 2);
        layout.Controls.Add(_chkActive, 1, row);
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
        layout.SetColumnSpan(btnRow, 3);
        layout.Controls.Add(btnRow, 0, row);

        this.Controls.Add(layout);
        this.AcceptButton = _btnSave;
        this.CancelButton = _btnCancel;
    }

    private void PopulateFields()
    {
        if (_rule.Id != 0)
        {
            _txtCrd.Text = _rule.FirmCrd;
            _lblFirmName.Text = _rule.FirmName ?? "";
            _cmbDirection.SelectedItem = _rule.ThresholdType;
            _txtThresholdAum.Text = _rule.ThresholdAmount.ToString("F0");
            _chkActive.Checked = _rule.IsActive;
        }
    }

    private void OnLookupFirm(object? sender, EventArgs e)
    {
        string crd = _txtCrd.Text.Trim();
        if (string.IsNullOrEmpty(crd))
        {
            MessageBox.Show("Enter a Firm CRD first.", "Lookup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? name = _firmNameLookup?.Invoke(crd);
        _lblFirmName.Text = name ?? "(not found)";
        _lblFirmName.ForeColor = name != null
            ? Color.FromArgb(60, 120, 60)
            : Color.FromArgb(180, 60, 60);
    }

    private void OnSave(object? sender, EventArgs e)
    {
        string crd = _txtCrd.Text.Trim();
        if (string.IsNullOrEmpty(crd))
        {
            MessageBox.Show("Firm CRD is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtCrd.Focus();
            return;
        }

        if (!decimal.TryParse(_txtThresholdAum.Text, out decimal threshold) || threshold <= 0)
        {
            MessageBox.Show("Enter a valid positive Threshold AUM.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtThresholdAum.Focus();
            return;
        }

        _rule.FirmCrd = crd;
        _rule.FirmName = string.IsNullOrWhiteSpace(_lblFirmName.Text) ? null : _lblFirmName.Text;
        _rule.ThresholdType = _cmbDirection.SelectedItem?.ToString() ?? "CrossAbove";
        _rule.ThresholdAmount = threshold;
        _rule.IsActive = _chkActive.Checked;

        try
        {
            _alertRepo.UpsertAumRule(_rule);
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
