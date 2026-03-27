using AdvisorLeads.Models;

namespace AdvisorLeads.Controls;

public class AdvisorDetailCard : UserControl
{
    private Advisor? _advisor;

    // Header section
    private Label _lblName = null!;
    private Label _lblCrd = null!;
    private Label _lblSourceBadge = null!;
    private Label _lblRecordTypeBadge = null!;
    private Label _lblStatusBadge = null!;
    private Label _lblCrmBadge = null!;
    private Label _lblExcludedBadge = null!;

    // Info grid
    private TableLayoutPanel _infoGrid = null!;

    // Tabbed sections
    private TabControl _tabs = null!;
    private ListView _lstEmployment = null!;
    private ListView _lstDisclosures = null!;
    private ListView _lstQualifications = null!;
    private ListView _lstRegistrations = null!;

    // Action buttons
    private Button _btnExclude = null!;
    private Button _btnRestore = null!;
    private Button _btnImportCrm = null!;
    private Button _btnRefresh = null!;
    private Button _btnAddToList = null!;
    private Button _btnFavorite = null!;

    public event EventHandler<Advisor>? ExcludeRequested;
    public event EventHandler<Advisor>? RestoreRequested;
    public event EventHandler<Advisor>? ImportCrmRequested;
    public event EventHandler<Advisor>? RefreshRequested;
    public event EventHandler<Advisor>? AddToListRequested;
    public event EventHandler<Advisor>? FavoriteRequested;

    public AdvisorDetailCard()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        this.BackColor = Color.White;
        this.Padding = new Padding(0);

        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16)
        };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(0)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // info
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // tabs
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // buttons

        // ── Header ─────────────────────────────────────────────────────
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 12),
            Height = 90
        };

        _lblName = new Label
        {
            Text = "Select an advisor",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 0),
            ForeColor = Color.FromArgb(30, 30, 80)
        };

        _lblCrd = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9),
            AutoSize = true,
            Location = new Point(0, 32),
            ForeColor = Color.Gray
        };

        var badgePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Location = new Point(0, 54),
            FlowDirection = FlowDirection.LeftToRight
        };

        _lblSourceBadge = MakeBadge("", Color.FromArgb(70, 130, 180));
        _lblRecordTypeBadge = MakeBadge("", Color.FromArgb(30, 140, 120));
        _lblStatusBadge = MakeBadge("", Color.FromArgb(60, 160, 80));
        _lblCrmBadge = MakeBadge("Wealthbox", Color.FromArgb(130, 80, 170));
        _lblExcludedBadge = MakeBadge("EXCLUDED", Color.FromArgb(200, 60, 60));

        badgePanel.Controls.AddRange(new Control[] { _lblSourceBadge, _lblRecordTypeBadge, _lblStatusBadge, _lblCrmBadge, _lblExcludedBadge });
        header.Controls.AddRange(new Control[] { _lblName, _lblCrd, badgePanel });
        mainLayout.Controls.Add(header, 0, 0);

        // ── Info Grid ─────────────────────────────────────────────────
        _infoGrid = new TableLayoutPanel
        {
            ColumnCount = 4,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 12)
        };
        _infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
        _infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        _infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
        _infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        mainLayout.Controls.Add(_infoGrid, 0, 1);

        // ── Tabs ──────────────────────────────────────────────────────
        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9)
        };

        // Employment tab
        var empPage = new TabPage("Employment History");
        _lstEmployment = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 9)
        };
        _lstEmployment.Columns.Add("Firm", -2);
        _lstEmployment.Columns.Add("CRD", 80);
        _lstEmployment.Columns.Add("Start", 90);
        _lstEmployment.Columns.Add("End", 90);
        _lstEmployment.Columns.Add("Position", 140);
        _lstEmployment.Columns.Add("Location", -2);
        empPage.Controls.Add(_lstEmployment);

        // Disclosures tab
        var discPage = new TabPage("Disclosures");
        _lstDisclosures = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 9)
        };
        _lstDisclosures.Columns.Add("Type", 130);
        _lstDisclosures.Columns.Add("Date", 90);
        _lstDisclosures.Columns.Add("Description", -2);
        _lstDisclosures.Columns.Add("Resolution", 120);
        discPage.Controls.Add(_lstDisclosures);

        // Qualifications tab
        var qualPage = new TabPage("Qualifications");
        _lstQualifications = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 9)
        };
        _lstQualifications.Columns.Add("Exam / Qualification", -2);
        _lstQualifications.Columns.Add("Code", 80);
        _lstQualifications.Columns.Add("Date", 100);
        _lstQualifications.Columns.Add("Status", 100);
        qualPage.Controls.Add(_lstQualifications);

        // Registrations tab
        var regPage = new TabPage("Registrations");
        _lstRegistrations = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 9)
        };
        _lstRegistrations.Columns.Add("State / Authority", -2);
        regPage.Controls.Add(_lstRegistrations);

        _tabs.TabPages.AddRange(new[] { empPage, discPage, qualPage, regPage });
        mainLayout.Controls.Add(_tabs, 0, 2);

        // ── Action Buttons ────────────────────────────────────────────
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        };

        _btnRefresh = MakeActionButton("Refresh Data", Color.FromArgb(70, 130, 180));
        _btnImportCrm = MakeActionButton("Import to Wealthbox", Color.FromArgb(130, 80, 170));
        _btnAddToList = MakeActionButton("Add to List", Color.FromArgb(60, 140, 60));
        _btnFavorite = MakeActionButton("☆ Favorite", Color.FromArgb(180, 130, 0));
        _btnExclude = MakeActionButton("Exclude", Color.FromArgb(200, 80, 60));
        _btnRestore = MakeActionButton("Restore", Color.FromArgb(60, 140, 70));

        _btnRefresh.Click += (_, _) => { if (_advisor != null) RefreshRequested?.Invoke(this, _advisor); };
        _btnImportCrm.Click += (_, _) => { if (_advisor != null) ImportCrmRequested?.Invoke(this, _advisor); };
        _btnAddToList.Click += (_, _) => { if (_advisor != null) AddToListRequested?.Invoke(this, _advisor); };
        _btnFavorite.Click += (_, _) => { if (_advisor != null) FavoriteRequested?.Invoke(this, _advisor); };
        _btnExclude.Click += (_, _) => { if (_advisor != null) ExcludeRequested?.Invoke(this, _advisor); };
        _btnRestore.Click += (_, _) => { if (_advisor != null) RestoreRequested?.Invoke(this, _advisor); };

        btnPanel.Controls.AddRange(new Control[] { _btnRefresh, _btnImportCrm, _btnAddToList, _btnFavorite, _btnExclude, _btnRestore });
        mainLayout.Controls.Add(btnPanel, 0, 3);

        outer.Controls.Add(mainLayout);
        this.Controls.Add(outer);

        // Start empty
        SetEmpty();
    }

    private void SetEmpty()
    {
        _lblSourceBadge.Visible = false;
        _lblRecordTypeBadge.Visible = false;
        _lblStatusBadge.Visible = false;
        _lblCrmBadge.Visible = false;
        _lblExcludedBadge.Visible = false;
        _btnRefresh.Enabled = false;
        _btnImportCrm.Enabled = false;
        _btnAddToList.Enabled = false;
        _btnFavorite.Enabled = false;
        _btnExclude.Enabled = false;
        _btnRestore.Enabled = false;
    }

    public void ShowAdvisor(Advisor advisor)
    {
        _advisor = advisor;

        _lblName.Text = advisor.FullName;
        _lblCrd.Text = $"CRD: {advisor.CrdNumber ?? "N/A"}"
                     + (advisor.IapdNumber != null ? $"  |  IAPD: {advisor.IapdNumber}" : "")
                     + (advisor.OtherNames != null ? $"  |  Also known as: {advisor.OtherNames}" : "")
                     + $"  |  Updated: {advisor.UpdatedAt:yyyy-MM-dd}";

        // Badges
        _lblSourceBadge.Text = advisor.Source ?? "";
        _lblSourceBadge.Visible = !string.IsNullOrEmpty(advisor.Source);

        _lblRecordTypeBadge.Text = advisor.RecordType ?? "";
        _lblRecordTypeBadge.Visible = !string.IsNullOrEmpty(advisor.RecordType);

        _lblStatusBadge.Text = advisor.RegistrationStatus ?? "";
        _lblStatusBadge.Visible = !string.IsNullOrEmpty(advisor.RegistrationStatus);

        _lblCrmBadge.Visible = advisor.IsImportedToCrm;
        _lblExcludedBadge.Visible = advisor.IsExcluded;

        // Info grid
        _infoGrid.Controls.Clear();
        _infoGrid.RowStyles.Clear();

        AddInfoRow("Firm:", advisor.CurrentFirmName ?? "—", "State:", advisor.State ?? "—");
        AddInfoRow("City:", $"{advisor.City ?? "—"}{(advisor.ZipCode != null ? " " + advisor.ZipCode : "")}", "Phone:", advisor.Phone ?? "—");
        AddInfoRow("Email:", advisor.Email ?? "—", "Title:", advisor.Title ?? "—");
        AddInfoRow("Licenses:", advisor.Licenses ?? "—", "Experience:", advisor.YearsOfExperience.HasValue ? $"{advisor.YearsOfExperience} years" : "—");
        AddInfoRow("Disclosures:", advisor.HasDisclosures ? $"Yes ({advisor.DisclosureCount})" : "No",
                   "Reg. Date:", advisor.RegistrationDate.HasValue ? advisor.RegistrationDate.Value.ToString("yyyy-MM-dd") : "—");
        if (!string.IsNullOrEmpty(advisor.RegAuthorities))
            AddInfoRow("Reg. Authorities:", advisor.RegAuthorities, "Disc. Flags:", advisor.DisclosureFlags ?? "—");
        else if (!string.IsNullOrEmpty(advisor.DisclosureFlags))
            AddInfoRow("Disc. Flags:", advisor.DisclosureFlags, "", "");
        if (!string.IsNullOrEmpty(advisor.ExclusionReason))
            AddInfoRow("Excluded Reason:", advisor.ExclusionReason, "", "");

        // Employment
        _lstEmployment.Items.Clear();
        _tabs.TabPages[0].Text = advisor.EmploymentHistory.Count > 0
            ? $"Employment ({advisor.EmploymentHistory.Count})"
            : "Employment History";
        foreach (var emp in advisor.EmploymentHistory)
        {
            var item = new ListViewItem(emp.FirmName);
            item.SubItems.Add(emp.FirmCrd ?? "");
            item.SubItems.Add(emp.StartDate?.ToString("yyyy-MM") ?? "");
            item.SubItems.Add(emp.IsCurrent ? "Current" : (emp.EndDate > DateTime.MinValue ? emp.EndDate!.Value.ToString("yyyy-MM") : ""));
            item.SubItems.Add(emp.Position ?? "");
            item.SubItems.Add(emp.Street ?? "");
            if (emp.IsCurrent)
                item.BackColor = Color.FromArgb(235, 248, 235);
            _lstEmployment.Items.Add(item);
        }

        // Disclosures
        _lstDisclosures.Items.Clear();
        _tabs.TabPages[1].Text = $"Disclosures ({advisor.Disclosures.Count})";
        foreach (var disc in advisor.Disclosures)
        {
            var item = new ListViewItem(disc.Type);
            item.SubItems.Add(disc.Date?.ToString("yyyy-MM-dd") ?? "");
            item.SubItems.Add(disc.Description ?? "");
            item.SubItems.Add(disc.Resolution ?? "");
            item.BackColor = Color.FromArgb(255, 248, 240);
            _lstDisclosures.Items.Add(item);
        }

        // Qualifications
        _lstQualifications.Items.Clear();
        _tabs.TabPages[2].Text = advisor.QualificationList.Count > 0
            ? $"Qualifications ({advisor.QualificationList.Count})"
            : "Qualifications";
        foreach (var qual in advisor.QualificationList)
        {
            var item = new ListViewItem(qual.Name);
            item.SubItems.Add(qual.Code ?? "");
            item.SubItems.Add(qual.Date?.ToString("yyyy-MM-dd") ?? "");
            item.SubItems.Add(qual.Status ?? "");
            _lstQualifications.Items.Add(item);
        }

        // Registrations (from RegAuthorities comma-joined state names)
        _lstRegistrations.Items.Clear();
        if (!string.IsNullOrEmpty(advisor.RegAuthorities))
        {
            foreach (var state in advisor.RegAuthorities.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                _lstRegistrations.Items.Add(state);
        }
        _tabs.TabPages[3].Text = _lstRegistrations.Items.Count > 0
            ? $"Registrations ({_lstRegistrations.Items.Count})"
            : "Registrations";

        // Buttons
        _btnRefresh.Enabled = !string.IsNullOrEmpty(advisor.CrdNumber);
        _btnImportCrm.Enabled = !advisor.IsExcluded;
        _btnAddToList.Enabled = true;
        _btnFavorite.Enabled = true;
        _btnFavorite.Text = advisor.IsFavorited ? "★ Unfavorite" : "☆ Favorite";
        _btnFavorite.BackColor = advisor.IsFavorited
            ? Color.FromArgb(255, 200, 0)
            : Color.FromArgb(180, 130, 0);
        _btnExclude.Enabled = !advisor.IsExcluded;
        _btnRestore.Enabled = advisor.IsExcluded;
        _btnImportCrm.Text = advisor.IsImportedToCrm ? "Re-import to Wealthbox" : "Import to Wealthbox";
    }

    private void AddInfoRow(string label1, string value1, string label2, string value2)
    {
        int row = _infoGrid.RowCount;
        _infoGrid.RowCount++;
        _infoGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _infoGrid.Controls.Add(MakeInfoLabel(label1), 0, row);
        _infoGrid.Controls.Add(MakeInfoValue(value1), 1, row);
        _infoGrid.Controls.Add(MakeInfoLabel(label2), 2, row);
        _infoGrid.Controls.Add(MakeInfoValue(value2), 3, row);
    }

    private static Label MakeInfoLabel(string text) => new Label
    {
        Text = text,
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleRight,
        ForeColor = Color.FromArgb(80, 80, 100),
        Padding = new Padding(0, 2, 4, 2)
    };

    private static Label MakeInfoValue(string text) => new Label
    {
        Text = text,
        Font = new Font("Segoe UI", 8.5f),
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(30, 30, 60),
        Padding = new Padding(2, 2, 0, 2)
    };

    private static Label MakeBadge(string text, Color color) => new Label
    {
        Text = text,
        Font = new Font("Segoe UI", 8, FontStyle.Bold),
        BackColor = color,
        ForeColor = Color.White,
        AutoSize = true,
        Padding = new Padding(6, 2, 6, 2),
        Margin = new Padding(0, 0, 4, 0)
    };

    private static Button MakeActionButton(string text, Color color)
    {
        var btn = new Button
        {
            Text = text,
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Height = 30,
            AutoSize = true,
            Font = new Font("Segoe UI", 9),
            Margin = new Padding(0, 0, 6, 0),
            Padding = new Padding(10, 0, 10, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }
}
