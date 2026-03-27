namespace AdvisorLeads.Forms;

/// <summary>
/// Modal dialog shown on first run (or after "Clear All Data") while the database is
/// populated from SEC/FINRA. Shows a scrollable log of all progress messages so the
/// user can see exactly what is happening and spot any errors.
///
/// The caller must supply the work via <see cref="WorkFactory"/> before calling ShowDialog.
/// The work is started from the dialog's Shown event so the window handle is guaranteed
/// to exist, which prevents the race condition where SetComplete fires before the form
/// is visible.
/// </summary>
public class InitialSetupDialog : Form
{
    private readonly Label _lblTitle;
    private readonly ListBox _logBox;
    private readonly ProgressBar _progressBar;
    private readonly Button _btnAction;
    private CancellationTokenSource? _cts;
    private bool _isDone;

    /// <summary>
    /// Set this before calling ShowDialog. The factory receives the progress reporter and
    /// cancellation token, and must return the number of records saved.
    /// </summary>
    public Func<IProgress<string>, CancellationToken, Task<int>>? WorkFactory { get; set; }

    public bool WasCancelled { get; private set; }

    public InitialSetupDialog()
    {
        Text = "AdvisorLeads – Initial Setup";
        Size = new Size(560, 420);
        MinimumSize = new Size(460, 340);
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ControlBox = false;
        Font = new Font("Segoe UI", 9);

        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(20, 16, 20, 14)
        };
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // title
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // log
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // progress bar
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // button

        _lblTitle = new Label
        {
            Text = "Setting up AdvisorLeads...",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(40, 60, 130),
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 8)
        };
        outer.Controls.Add(_lblTitle, 0, 0);

        _logBox = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 8.5f),
            BackColor = Color.FromArgb(248, 248, 252),
            BorderStyle = BorderStyle.FixedSingle,
            HorizontalScrollbar = true,
            ScrollAlwaysVisible = false
        };
        outer.Controls.Add(_logBox, 0, 1);

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30
        };
        outer.Controls.Add(_progressBar, 0, 2);

        _btnAction = new Button
        {
            Text = "Skip (use empty database)",
            Width = 200,
            Anchor = AnchorStyles.Right,
            Padding = new Padding(8, 2, 8, 2)
        };
        _btnAction.Click += OnActionClick;
        outer.Controls.Add(_btnAction, 0, 3);

        Controls.Add(outer);
        this.Shown += OnShown;
    }

    private void OnActionClick(object? sender, EventArgs e)
    {
        if (_isDone)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            WasCancelled = true;
            _cts?.Cancel();
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private void OnShown(object? sender, EventArgs e)
    {
        if (WorkFactory == null) return;

        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(msg => AppendLog(msg));

        _ = Task.Run(async () =>
        {
            try
            {
                var count = await WorkFactory(progress, _cts.Token);
                SetComplete(count);
            }
            catch (OperationCanceledException)
            {
                // user skipped
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: {ex.Message}");
                SetComplete(-1);
            }
        });
    }

    /// <summary>
    /// Appends a message to the log. Thread-safe.
    /// </summary>
    public void AppendLog(string message)
    {
        if (!IsHandleCreated) return;
        if (InvokeRequired) { BeginInvoke(() => AppendLog(message)); return; }
        _logBox.Items.Add(message);
        _logBox.TopIndex = _logBox.Items.Count - 1;

        // Also write to startup log for diagnostic purposes
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AdvisorLeads");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, "startup.log"),
                $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    /// <summary>Called when population is complete. Thread-safe.</summary>
    public void SetComplete(int count)
    {
        if (!IsHandleCreated) return;
        if (InvokeRequired) { BeginInvoke(() => SetComplete(count)); return; }

        _isDone = true;
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Value = 100;

        if (count < 0)
        {
            _lblTitle.Text = "Setup encountered errors";
            _lblTitle.ForeColor = Color.DarkRed;
            AppendLog("⚠ Setup finished with errors. See log above. You can fetch data later via Data → Fetch New Data.");
        }
        else
        {
            _lblTitle.Text = "Setup Complete";
            AppendLog($"✓ {count} advisor records loaded. FINRA broker-dealer data will sync in the background.");
        }

        _btnAction.Text = "Continue";
    }

    /// <summary>Kept for backwards compatibility – delegates to AppendLog.</summary>
    public void SetProgress(string message) => AppendLog(message);

    public void SetCancellationToken(CancellationTokenSource cts) => _cts = cts;
}
