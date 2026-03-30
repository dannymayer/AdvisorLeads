using AdvisorLeads.Data;

namespace AdvisorLeads.Controls;

public class DashboardPanel : UserControl
{
    private readonly AdvisorRepository _repo;

    private Label _lblTotalAdvisors = null!;
    private Label _lblTotalFirms = null!;
    private Label _lblDisclosurePct = null!;
    private Label _lblLastSync = null!;
    private Label _lblFinraCount = null!;
    private Label _lblSecCount = null!;
    private Label _lblFavorites = null!;
    private Label _lblInCrm = null!;

    public event EventHandler? RefreshDataRequested;
    public event EventHandler? DataQualityCheckRequested;
    public event EventHandler? BrowseAdvisorsRequested;
    public event EventHandler? BrowseFirmsRequested;
    public event EventHandler? BrowseReportsRequested;

    public DashboardPanel(AdvisorRepository repo)
    {
        _repo = repo;
        BuildUI();
        this.Load += async (_, _) => await LoadStatsAsync();
    }

    private void BuildUI()
    {
        this.BackColor = Color.FromArgb(245, 247, 252);
        this.Padding = new Padding(16);
        this.AutoScroll = true;

        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblTitle = new Label
        {
            Text = "Dashboard",
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 40, 100),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16)
        };
        outer.Controls.Add(lblTitle, 0, 0);

        // ── Stats grid ──
        var statsGrid = new TableLayoutPanel
        {
            ColumnCount = 4,
            RowCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16)
        };
        statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        _lblTotalAdvisors = MakeStatValue("—");
        _lblTotalFirms = MakeStatValue("—");
        _lblDisclosurePct = MakeStatValue("—");
        _lblLastSync = MakeStatValue("—");
        _lblFinraCount = MakeStatValue("—");
        _lblSecCount = MakeStatValue("—");
        _lblFavorites = MakeStatValue("—");
        _lblInCrm = MakeStatValue("—");

        statsGrid.Controls.Add(MakeStatCard("Total Advisors", _lblTotalAdvisors, Color.FromArgb(0, 100, 200)), 0, 0);
        statsGrid.Controls.Add(MakeStatCard("Total Firms", _lblTotalFirms, Color.FromArgb(0, 140, 100)), 1, 0);
        statsGrid.Controls.Add(MakeStatCard("With Disclosures", _lblDisclosurePct, Color.FromArgb(200, 100, 0)), 2, 0);
        statsGrid.Controls.Add(MakeStatCard("Last Sync", _lblLastSync, Color.FromArgb(80, 80, 180)), 3, 0);
        statsGrid.Controls.Add(MakeStatCard("FINRA Advisors", _lblFinraCount, Color.FromArgb(70, 130, 180)), 0, 1);
        statsGrid.Controls.Add(MakeStatCard("Inv. Advisors", _lblSecCount, Color.FromArgb(60, 160, 100)), 1, 1);
        statsGrid.Controls.Add(MakeStatCard("Favorites", _lblFavorites, Color.FromArgb(180, 130, 0)), 2, 1);
        statsGrid.Controls.Add(MakeStatCard("In CRM", _lblInCrm, Color.FromArgb(130, 80, 170)), 3, 1);

        outer.Controls.Add(statsGrid, 0, 1);

        // ── Buttons ──
        var btnPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 8, 0, 0)
        };

        var btnRefresh = new Button
        {
            Text = "Refresh Data",
            BackColor = Color.FromArgb(0, 100, 200),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10),
            Height = 36,
            AutoSize = true,
            Padding = new Padding(12, 0, 12, 0),
            Margin = new Padding(0, 0, 8, 0)
        };
        btnRefresh.FlatAppearance.BorderSize = 0;
        btnRefresh.Click += (_, _) => RefreshDataRequested?.Invoke(this, EventArgs.Empty);

        var btnQuality = new Button
        {
            Text = "Run Data Quality Check",
            BackColor = Color.FromArgb(60, 140, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10),
            Height = 36,
            AutoSize = true,
            Padding = new Padding(12, 0, 12, 0),
            Margin = new Padding(0, 0, 8, 0)
        };
        btnQuality.FlatAppearance.BorderSize = 0;
        btnQuality.Click += (_, _) => DataQualityCheckRequested?.Invoke(this, EventArgs.Empty);

        var btnBrowseAdvisors = new Button
        {
            Text = "Browse Advisors →",
            BackColor = Color.White,
            ForeColor = Color.FromArgb(0, 100, 200),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10),
            Height = 36,
            AutoSize = true,
            Padding = new Padding(12, 0, 12, 0),
            Margin = new Padding(0, 0, 8, 0)
        };
        btnBrowseAdvisors.FlatAppearance.BorderColor = Color.FromArgb(0, 100, 200);
        btnBrowseAdvisors.FlatAppearance.BorderSize = 1;
        btnBrowseAdvisors.Click += (_, _) => BrowseAdvisorsRequested?.Invoke(this, EventArgs.Empty);

        var btnBrowseFirms = new Button
        {
            Text = "Browse Firms →",
            BackColor = Color.White,
            ForeColor = Color.FromArgb(0, 140, 100),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10),
            Height = 36,
            AutoSize = true,
            Padding = new Padding(12, 0, 12, 0),
            Margin = new Padding(0, 0, 8, 0)
        };
        btnBrowseFirms.FlatAppearance.BorderColor = Color.FromArgb(0, 140, 100);
        btnBrowseFirms.FlatAppearance.BorderSize = 1;
        btnBrowseFirms.Click += (_, _) => BrowseFirmsRequested?.Invoke(this, EventArgs.Empty);

        var btnBrowseReports = new Button
        {
            Text = "📊 View Reports →",
            BackColor = Color.White,
            ForeColor = Color.FromArgb(60, 160, 100),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10),
            Height = 36,
            AutoSize = true,
            Padding = new Padding(12, 0, 12, 0),
            Margin = new Padding(0, 0, 8, 0)
        };
        btnBrowseReports.FlatAppearance.BorderColor = Color.FromArgb(60, 160, 100);
        btnBrowseReports.FlatAppearance.BorderSize = 1;
        btnBrowseReports.Click += (_, _) => BrowseReportsRequested?.Invoke(this, EventArgs.Empty);

        btnPanel.Controls.Add(btnRefresh);
        btnPanel.Controls.Add(btnQuality);
        btnPanel.Controls.Add(btnBrowseAdvisors);
        btnPanel.Controls.Add(btnBrowseFirms);
        btnPanel.Controls.Add(btnBrowseReports);
        outer.Controls.Add(btnPanel, 0, 2);

        this.Controls.Add(outer);
    }

    private static Panel MakeStatCard(string title, Label valueLabel, Color accentColor)
    {
        var card = new Panel
        {
            BackColor = Color.White,
            BorderStyle = BorderStyle.None,
            Margin = new Padding(0, 0, 12, 12),
            MinimumSize = new Size(160, 84),
            Padding = new Padding(16, 10, 16, 10)
        };

        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(220, 225, 235), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            using var accentBrush = new SolidBrush(accentColor);
            e.Graphics.FillRectangle(accentBrush, 0, 0, 4, card.Height);
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 110, 130),
            AutoSize = true,
            Location = new Point(20, 8)
        };

        valueLabel.Location = new Point(20, 32);
        valueLabel.ForeColor = accentColor;
        card.Controls.Add(lblTitle);
        card.Controls.Add(valueLabel);
        return card;
    }

    private static Label MakeStatValue(string initial) => new Label
    {
        Text = initial,
        Font = new Font("Segoe UI", 20, FontStyle.Bold),
        AutoSize = true
    };

    public async Task LoadStatsAsync()
    {
        try
        {
            var (total, withDisc, pct, finra, sec, favs, inCrm) = await Task.Run(() => _repo.GetAdvisorStats());
            var (totalFirms, _, _) = await Task.Run(() => _repo.GetFirmStats());

            if (InvokeRequired)
                BeginInvoke(() => ApplyStats(total, withDisc, pct, finra, sec, favs, inCrm, totalFirms));
            else
                ApplyStats(total, withDisc, pct, finra, sec, favs, inCrm, totalFirms);
        }
        catch { /* non-critical */ }
    }

    private void ApplyStats(int total, int withDisc, double pct, int finra, int sec, int favs, int inCrm, int totalFirms)
    {
        _lblTotalAdvisors.Text = total.ToString("N0");
        _lblTotalFirms.Text = totalFirms.ToString("N0");
        _lblDisclosurePct.Text = $"{pct:F1}%";
        _lblFinraCount.Text = finra.ToString("N0");
        _lblSecCount.Text = sec.ToString("N0");
        _lblFavorites.Text = favs.ToString("N0");
        _lblInCrm.Text = inCrm.ToString("N0");
    }

    public void UpdateLastSync(DateTime? lastSync)
    {
        if (!IsHandleCreated) { return; }
        if (InvokeRequired) { BeginInvoke(() => UpdateLastSync(lastSync)); return; }

        _lblLastSync.Text = lastSync.HasValue
            ? lastSync.Value.ToString("M/d h:mm tt")
            : "Never";
    }
}
