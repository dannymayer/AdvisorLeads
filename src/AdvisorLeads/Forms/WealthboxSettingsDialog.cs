namespace AdvisorLeads.Forms;

public class WealthboxSettingsDialog : Form
{
    private TextBox _txtToken = null!;
    private Button _btnValidate = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;
    private Label _lblStatus = null!;

    public string AccessToken => _txtToken.Text.Trim();

    public WealthboxSettingsDialog(string currentToken = "")
    {
        BuildUI(currentToken);
    }

    private void BuildUI(string currentToken)
    {
        this.Text = "Wealthbox CRM Settings";
        this.Width = 440;
        this.Height = 220;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.Font = new Font("Segoe UI", 9);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(16)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        var lblTitle = new Label
        {
            Text = "Wealthbox API Settings",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(100, 50, 160)
        };
        layout.SetColumnSpan(lblTitle, 2);
        layout.Controls.Add(lblTitle, 0, row++);

        var lblDesc = new Label
        {
            Text = "Enter your Wealthbox API access token from\nSettings → API Tokens in Wealthbox.",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray,
            AutoSize = false,
            Height = 36
        };
        layout.SetColumnSpan(lblDesc, 2);
        layout.Controls.Add(lblDesc, 0, row++);

        layout.Controls.Add(new Label { Text = "Access Token:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, row);
        _txtToken = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = currentToken,
            PasswordChar = '●',
            PlaceholderText = "Paste your API token here"
        };
        layout.Controls.Add(_txtToken, 1, row++);

        _lblStatus = new Label
        {
            Text = "",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray
        };
        layout.SetColumnSpan(_lblStatus, 2);
        layout.Controls.Add(_lblStatus, 0, row++);

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        _btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80, Height = 28 };
        _btnSave = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Width = 80,
            Height = 28,
            BackColor = Color.FromArgb(100, 50, 160),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnSave.FlatAppearance.BorderSize = 0;

        _btnValidate = new Button { Text = "Validate", Width = 80, Height = 28 };
        _btnValidate.Click += OnValidate;

        btnPanel.Controls.Add(_btnCancel);
        btnPanel.Controls.Add(_btnSave);
        btnPanel.Controls.Add(_btnValidate);
        layout.SetColumnSpan(btnPanel, 2);
        layout.Controls.Add(btnPanel, 0, row++);

        this.Controls.Add(layout);
        this.AcceptButton = _btnSave;
        this.CancelButton = _btnCancel;
    }

    private async void OnValidate(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtToken.Text))
        {
            _lblStatus.Text = "Please enter an access token.";
            _lblStatus.ForeColor = Color.OrangeRed;
            return;
        }

        _btnValidate.Enabled = false;
        _lblStatus.Text = "Validating...";
        _lblStatus.ForeColor = Color.DimGray;

        var svc = new Services.WealthboxService(_txtToken.Text.Trim());
        bool valid = await svc.ValidateTokenAsync();

        _btnValidate.Enabled = true;
        if (valid)
        {
            _lblStatus.Text = "✓ Token is valid!";
            _lblStatus.ForeColor = Color.Green;
        }
        else
        {
            _lblStatus.Text = "✗ Token validation failed. Check your token.";
            _lblStatus.ForeColor = Color.OrangeRed;
        }
    }
}
