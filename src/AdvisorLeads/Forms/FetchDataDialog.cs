namespace AdvisorLeads.Forms;

public class FetchDataDialog : Form
{
    private TextBox _txtQuery = null!;
    private ComboBox _cboState = null!;
    private CheckBox _chkFinra = null!;
    private CheckBox _chkSec = null!;
    private Button _btnFetch = null!;
    private Button _btnCancel = null!;
    private ProgressBar _progress = null!;
    private Label _lblStatus = null!;
    private RichTextBox _rtbLog = null!;

    public string SearchQuery => _txtQuery.Text;
    public string? SelectedState => _cboState.SelectedIndex > 0 ? _cboState.SelectedItem?.ToString() : null;
    public bool IncludeFinra => _chkFinra.Checked;
    public bool IncludeSec => _chkSec.Checked;

    public FetchDataDialog()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        this.Text = "Fetch New Data";
        this.Size = new Size(480, 440);
        this.MinimumSize = new Size(400, 360);
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.Font = new Font("Segoe UI", 9);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(16),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        // Title
        var lblTitle = new Label
        {
            Text = "Fetch Data from FINRA / SEC IAPD",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(40, 60, 130)
        };
        layout.SetColumnSpan(lblTitle, 2);
        layout.Controls.Add(lblTitle, 0, row++);

        // Query
        layout.Controls.Add(MakeLabel("Search query:"), 0, row);
        _txtQuery = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "e.g. John Smith or leave blank for broad search" };
        layout.Controls.Add(_txtQuery, 1, row++);

        // State
        layout.Controls.Add(MakeLabel("State filter:"), 0, row);
        _cboState = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboState.Items.Add("(All states)");
        var states = new[] { "AL","AK","AZ","AR","CA","CO","CT","DE","FL","GA","HI","ID","IL","IN",
            "IA","KS","KY","LA","ME","MD","MA","MI","MN","MS","MO","MT","NE","NV","NH","NJ","NM",
            "NY","NC","ND","OH","OK","OR","PA","RI","SC","SD","TN","TX","UT","VT","VA","WA","WV","WI","WY" };
        _cboState.Items.AddRange(states);
        _cboState.SelectedIndex = 0;
        layout.Controls.Add(_cboState, 1, row++);

        // Source checkboxes
        layout.Controls.Add(MakeLabel("Sources:"), 0, row);
        var srcPanel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        _chkFinra = new CheckBox { Text = "FINRA BrokerCheck", Checked = true, AutoSize = true };
        _chkSec = new CheckBox { Text = "SEC IAPD", Checked = true, AutoSize = true };
        srcPanel.Controls.Add(_chkFinra);
        srcPanel.Controls.Add(_chkSec);
        layout.Controls.Add(srcPanel, 1, row++);

        // Progress bar
        _progress = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Style = ProgressBarStyle.Marquee,
            Visible = false,
            Height = 12
        };
        layout.SetColumnSpan(_progress, 2);
        layout.Controls.Add(_progress, 0, row++);

        // Status label
        _lblStatus = new Label
        {
            Text = "Enter a search query and click Fetch.",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray,
            AutoSize = false,
            Height = 20
        };
        layout.SetColumnSpan(_lblStatus, 2);
        layout.Controls.Add(_lblStatus, 0, row++);

        // Log
        _rtbLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(248, 248, 252),
            Font = new Font("Consolas", 8.5f),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Height = 100
        };
        layout.SetColumnSpan(_rtbLog, 2);
        layout.Controls.Add(_rtbLog, 0, row++);

        // Buttons
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        };

        _btnCancel = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.Cancel,
            Width = 80,
            Height = 30
        };
        _btnFetch = new Button
        {
            Text = "Fetch",
            DialogResult = DialogResult.None,
            Width = 80,
            Height = 30,
            BackColor = Color.FromArgb(70, 130, 180),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnFetch.FlatAppearance.BorderSize = 0;
        _btnFetch.Click += OnFetch;

        btnPanel.Controls.Add(_btnCancel);
        btnPanel.Controls.Add(_btnFetch);
        layout.SetColumnSpan(btnPanel, 2);
        layout.Controls.Add(btnPanel, 0, row++);

        this.Controls.Add(layout);
        this.AcceptButton = _btnFetch;
        this.CancelButton = _btnCancel;
    }

    private async void OnFetch(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtQuery.Text))
        {
            MessageBox.Show("Please enter a search query.", "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnFetch.Enabled = false;
        _progress.Visible = true;
        _rtbLog.Clear();

        // Signal the parent form by raising the event
        FetchRequested?.Invoke(this, new FetchRequestedEventArgs(
            _txtQuery.Text.Trim(),
            SelectedState,
            IncludeFinra,
            IncludeSec
        ));
    }

    public event EventHandler<FetchRequestedEventArgs>? FetchRequested;

    public void SetProgress(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetProgress(message));
            return;
        }
        _lblStatus.Text = message;
        _rtbLog.AppendText(message + Environment.NewLine);
        _rtbLog.ScrollToCaret();
    }

    public void FetchComplete(int newCount, int updatedCount)
    {
        if (InvokeRequired)
        {
            Invoke(() => FetchComplete(newCount, updatedCount));
            return;
        }
        int total = newCount + updatedCount;
        _progress.Visible = false;
        _btnFetch.Enabled = true;
        _lblStatus.Text = total == 0
            ? "Done! No records matched the query."
            : $"Done! {total} synced — {newCount} new, {updatedCount} updated.";
        _rtbLog.AppendText(total == 0
            ? $"✓ Completed: No records found.{Environment.NewLine}"
            : $"✓ Completed: {total} synced ({newCount} new, {updatedCount} updated).{Environment.NewLine}");
    }

    public void FetchFailed(string error)
    {
        if (InvokeRequired)
        {
            Invoke(() => FetchFailed(error));
            return;
        }
        _progress.Visible = false;
        _btnFetch.Enabled = true;
        _lblStatus.Text = $"Error: {error}";
        _rtbLog.AppendText($"✗ Error: {error}{Environment.NewLine}");
    }

    private static Label MakeLabel(string text) => new Label
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleRight
    };
}

public class FetchRequestedEventArgs : EventArgs
{
    public string Query { get; }
    public string? State { get; }
    public bool IncludeFinra { get; }
    public bool IncludeSec { get; }

    public FetchRequestedEventArgs(string query, string? state, bool includeFinra, bool includeSec)
    {
        Query = query;
        State = state;
        IncludeFinra = includeFinra;
        IncludeSec = includeSec;
    }
}
