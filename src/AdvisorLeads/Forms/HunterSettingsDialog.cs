namespace AdvisorLeads.Forms;

public class HunterSettingsDialog : Form
{
    private TextBox _txtApiKey = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;

    public string ApiKey => _txtApiKey.Text.Trim();

    public HunterSettingsDialog(string currentKey = "")
    {
        BuildUI(currentKey);
    }

    private void BuildUI(string currentKey)
    {
        this.Text = "Hunter.io Settings";
        this.Size = new Size(440, 200);
        this.MinimumSize = new Size(380, 180);
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.Font = new Font("Segoe UI", 9);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(16)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        var lblTitle = new Label
        {
            Text = "Hunter.io API Settings",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(0, 120, 160)
        };
        layout.SetColumnSpan(lblTitle, 2);
        layout.Controls.Add(lblTitle, 0, row++);

        var lblDesc = new Label
        {
            Text = "Enter your Hunter.io API key from\nhttps://hunter.io/api-keys",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray,
            AutoSize = false,
            Height = 36
        };
        layout.SetColumnSpan(lblDesc, 2);
        layout.Controls.Add(lblDesc, 0, row++);

        layout.Controls.Add(new Label { Text = "API Key:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, row);
        _txtApiKey = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = currentKey,
            PasswordChar = '●',
            PlaceholderText = "Paste your API key here"
        };
        layout.Controls.Add(_txtApiKey, 1, row++);

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
            BackColor = Color.FromArgb(0, 120, 160),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnSave.FlatAppearance.BorderSize = 0;

        btnPanel.Controls.Add(_btnCancel);
        btnPanel.Controls.Add(_btnSave);
        layout.SetColumnSpan(btnPanel, 2);
        layout.Controls.Add(btnPanel, 0, row++);

        this.Controls.Add(layout);
        this.AcceptButton = _btnSave;
        this.CancelButton = _btnCancel;
    }
}
