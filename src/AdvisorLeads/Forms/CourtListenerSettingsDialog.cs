using System.Windows.Forms;

namespace AdvisorLeads.Forms;

/// <summary>
/// Simple settings dialog for configuring the CourtListener API token.
/// An API token enables higher rate limits (5,000 req/day vs 100/day anonymous).
/// </summary>
public class CourtListenerSettingsDialog : Form
{
    private TextBox _txtToken = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;
    private LinkLabel _lnkSite = null!;

    public string ApiToken { get; private set; } = string.Empty;

    public CourtListenerSettingsDialog(string? existingToken = null)
    {
        InitializeComponent();
        _txtToken.Text = existingToken ?? string.Empty;
    }

    private void InitializeComponent()
    {
        Text = "CourtListener Settings";
        Width = 480;
        Height = 220;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new System.Drawing.Font("Segoe UI", 9f);

        var lblInfo = new Label
        {
            Text = "Enter your CourtListener API token to enable higher search limits (5,000 req/day).\n"
                 + "Leave blank to use anonymous access (100 req/day).",
            Left = 12, Top = 12, Width = 440, Height = 38,
            AutoSize = false
        };

        var lblToken = new Label
        {
            Text = "API Token:", Left = 12, Top = 60, Width = 80, AutoSize = true
        };

        _txtToken = new TextBox
        {
            Left = 100, Top = 57, Width = 352,
            UseSystemPasswordChar = false
        };

        _lnkSite = new LinkLabel
        {
            Text = "Get a token at courtlistener.com",
            Left = 12, Top = 88, AutoSize = true
        };
        _lnkSite.LinkClicked += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://www.courtlistener.com/api/",
                UseShellExecute = true
            });

        _btnSave = new Button
        {
            Text = "Save",
            Left = 277, Top = 140, Width = 80, Height = 28,
            DialogResult = DialogResult.OK
        };
        _btnSave.Click += (_, _) =>
        {
            ApiToken = _txtToken.Text.Trim();
            DialogResult = DialogResult.OK;
            Close();
        };

        _btnCancel = new Button
        {
            Text = "Cancel",
            Left = 372, Top = 140, Width = 80, Height = 28,
            DialogResult = DialogResult.Cancel
        };

        Controls.AddRange(new Control[] { lblInfo, lblToken, _txtToken, _lnkSite, _btnSave, _btnCancel });
        AcceptButton = _btnSave;
        CancelButton = _btnCancel;
    }
}
