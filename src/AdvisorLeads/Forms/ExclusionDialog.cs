namespace AdvisorLeads.Forms;

public class ExclusionDialog : Form
{
    private TextBox _txtReason = null!;
    private Button _btnOk = null!;
    private Button _btnCancel = null!;

    public string Reason => _txtReason.Text.Trim();

    public ExclusionDialog(string advisorName)
    {
        BuildUI(advisorName);
    }

    private void BuildUI(string advisorName)
    {
        this.Text = "Exclude Advisor";
        this.Size = new Size(400, 200);
        this.MinimumSize = new Size(340, 180);
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.Font = new Font("Segoe UI", 9);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16)
        };

        layout.Controls.Add(new Label
        {
            Text = $"Exclude \"{advisorName}\" from results?",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = "Reason (optional):",
            Dock = DockStyle.Fill
        }, 0, 1);

        _txtReason = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "e.g. Not a fit, already contacted, etc."
        };
        layout.Controls.Add(_txtReason, 0, 2);

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        };

        _btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80, Height = 28 };
        _btnOk = new Button
        {
            Text = "Exclude",
            DialogResult = DialogResult.OK,
            Width = 80,
            Height = 28,
            BackColor = Color.FromArgb(200, 60, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnOk.FlatAppearance.BorderSize = 0;

        btnPanel.Controls.Add(_btnCancel);
        btnPanel.Controls.Add(_btnOk);
        layout.Controls.Add(btnPanel, 0, 3);

        this.Controls.Add(layout);
        this.AcceptButton = _btnOk;
        this.CancelButton = _btnCancel;
    }
}
