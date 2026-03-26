namespace AdvisorLeads.Forms;

/// <summary>
/// Modal dialog shown on first run while the database is being populated with
/// advisor data from FINRA BrokerCheck. Shows progress messages and prevents
/// interaction with the main form until population completes.
/// </summary>
public class InitialSetupDialog : Form
{
    private readonly Label _lblTitle;
    private readonly Label _lblStatus;
    private readonly ProgressBar _progressBar;
    private readonly Button _btnCancel;
    private CancellationTokenSource? _cts;

    public bool WasCancelled { get; private set; }

    public InitialSetupDialog()
    {
        Text = "AdvisorLeads – Initial Setup";
        Width = 480;
        Height = 220;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ControlBox = false;
        Font = new Font("Segoe UI", 9);

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24, 20, 24, 16)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblTitle = new Label
        {
            Text = "Setting up AdvisorLeads for first use...",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(40, 60, 130),
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 8)
        };
        panel.Controls.Add(_lblTitle, 0, 0);

        _lblStatus = new Label
        {
            Text = "Fetching advisor data from FINRA BrokerCheck.\nThis only needs to happen once.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Padding = new Padding(0, 0, 0, 12)
        };
        panel.Controls.Add(_lblStatus, 0, 1);

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Height = 24
        };
        panel.Controls.Add(_progressBar, 0, 2);

        _btnCancel = new Button
        {
            Text = "Skip (use empty database)",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Padding = new Padding(8, 2, 8, 2)
        };
        _btnCancel.Click += (_, _) =>
        {
            WasCancelled = true;
            _cts?.Cancel();
            DialogResult = DialogResult.Cancel;
            Close();
        };
        panel.Controls.Add(_btnCancel, 0, 3);

        Controls.Add(panel);
    }

    /// <summary>
    /// Updates the status text from the background data fetch.
    /// Thread-safe: marshals to UI thread automatically.
    /// </summary>
    public void SetProgress(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetProgress(message));
            return;
        }
        _lblStatus.Text = message;
    }

    /// <summary>
    /// Called when population is complete. Closes the dialog with OK result.
    /// </summary>
    public void SetComplete(int count)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetComplete(count));
            return;
        }
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Value = 100;
        _lblTitle.Text = "Setup Complete";
        _lblStatus.Text = $"✓ {count} advisors loaded. The application is ready.";
        _btnCancel.Text = "Continue";
        _btnCancel.Click -= null!;
        _btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };
    }

    public void SetCancellationToken(CancellationTokenSource cts) => _cts = cts;
}
