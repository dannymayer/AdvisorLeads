namespace AdvisorLeads.Forms;

public class CreateListDialog : Form
{
    private TextBox _txtName = null!;
    private TextBox _txtDesc = null!;
    private Button _btnOk = null!;
    private Button _btnCancel = null!;

    public string ListName => _txtName.Text.Trim();
    public string? ListDescription => string.IsNullOrWhiteSpace(_txtDesc.Text) ? null : _txtDesc.Text.Trim();

    public CreateListDialog(string? existingName = null, string? existingDescription = null)
    {
        Text = existingName == null ? "Create New List" : "Edit List";
        Size = new Size(380, 200);
        MinimumSize = new Size(320, 180);
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Name:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
        _txtName = new TextBox { Dock = DockStyle.Fill, Text = existingName ?? "" };
        layout.Controls.Add(_txtName, 1, 0);

        layout.Controls.Add(new Label { Text = "Description:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 1);
        _txtDesc = new TextBox { Dock = DockStyle.Fill, Text = existingDescription ?? "", Multiline = true, Height = 50 };
        layout.Controls.Add(_txtDesc, 1, 1);

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        _btnCancel = new Button { Text = "Cancel", Width = 70, Height = 28 };
        _btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _btnOk = new Button
        {
            Text = "OK",
            Width = 70,
            Height = 28,
            BackColor = Color.FromArgb(70, 100, 180),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnOk.FlatAppearance.BorderSize = 0;
        _btnOk.Click += OnOk;
        btnPanel.Controls.AddRange(new Control[] { _btnCancel, _btnOk });
        layout.SetColumnSpan(btnPanel, 2);
        layout.Controls.Add(btnPanel, 0, 3);

        Controls.Add(layout);
        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
        _txtName.Focus();
    }

    private void OnOk(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show("Please enter a list name.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
