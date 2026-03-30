using AdvisorLeads.Models;

namespace AdvisorLeads.Controls;

public class AdvisorDetailCard : UserControl
{
    private Advisor? _advisor;

    // Header section
    private Label _lblName = null!;
    private Label _lblCrd = null!;
    private LinkLabel _lnkProfiles = null!;
    private Label _lblSourceBadge = null!;
    private Label _lblRecordTypeBadge = null!;
    private Label _lblStatusBadge = null!;
    private Label _lblCrmBadge = null!;
    private Label _lblExcludedBadge = null!;
    private Label _lblBcScopeBadge = null!;
    private Label _lblIaScopeBadge = null!;

    // Info grid
    private TableLayoutPanel _infoGrid = null!;

    // Tabbed sections
    private TabControl _tabs = null!;
    private ListView _lstEmployment = null!;
    private ListView _lstDisclosures = null!;
    private ListView _lstQualifications = null!;
    private ListView _lstRegistrations = null!;
    private Panel _firmDetailsPanel = null!;

    // Action buttons
    private Button _btnExclude = null!;
    private Button _btnRestore = null!;
    private Button _btnImportCrm = null!;
    private Button _btnRefresh = null!;
    private Button _btnAddToList = null!;
    private Button _btnFavorite = null!;
    private Button _btnFindEmail = null!;
    private Button _btnWatch = null!;

    public event EventHandler<Advisor>? ExcludeRequested;
    public event EventHandler<Advisor>? RestoreRequested;
    public event EventHandler<Advisor>? ImportCrmRequested;
    public event EventHandler<Advisor>? RefreshRequested;
    public event EventHandler<Advisor>? AddToListRequested;
    public event EventHandler<Advisor>? FavoriteRequested;
    public event EventHandler<Advisor>? FindEmailRequested;
    public event EventHandler<string>? FirmNavigationRequested;
    public event EventHandler<Advisor>? WatchToggleRequested;

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
        // TableLayoutPanel with AutoSize rows so the header grows with content
        // rather than clipping badges when the name wraps or the window is small.
        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 0, 0, 12)
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // name
        headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // crd
        headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // profile links
        headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // badges

        _lblName = new Label
        {
            Text = "Select an advisor",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            ForeColor = Color.FromArgb(30, 30, 80)
        };

        _lblCrd = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        _lnkProfiles = new LinkLabel
        {
            Text = "",
            Font = new Font("Segoe UI", 8.5f),
            AutoSize = true,
            Visible = false
        };
        _lnkProfiles.LinkClicked += (s, e) =>
        {
            if (e.Link?.LinkData is string url)
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
                catch { }
            }
        };

        var badgePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight
        };

        _lblSourceBadge = MakeBadge("", Color.FromArgb(70, 130, 180));
        _lblRecordTypeBadge = MakeBadge("", Color.FromArgb(30, 140, 120));
        _lblStatusBadge = MakeBadge("", Color.FromArgb(60, 160, 80));
        _lblCrmBadge = MakeBadge("Wealthbox", Color.FromArgb(130, 80, 170));
        _lblExcludedBadge = MakeBadge("EXCLUDED", Color.FromArgb(200, 60, 60));
        _lblBcScopeBadge = MakeBadge("", Color.FromArgb(50, 100, 160));
        _lblIaScopeBadge = MakeBadge("", Color.FromArgb(20, 120, 100));

        _btnWatch = new Button
        {
            Text = "Watch",
            AutoSize = true,
            Height = 22,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(200, 190, 50),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Padding = new Padding(6, 0, 6, 0),
            Enabled = false
        };
        _btnWatch.FlatAppearance.BorderSize = 0;
        _btnWatch.Click += (_, _) => { if (_advisor != null) WatchToggleRequested?.Invoke(this, _advisor); };

        badgePanel.Controls.AddRange(new Control[] { _lblSourceBadge, _lblRecordTypeBadge, _lblStatusBadge, _lblBcScopeBadge, _lblIaScopeBadge, _lblCrmBadge, _lblExcludedBadge, _btnWatch });
        headerLayout.Controls.Add(_lblName, 0, 0);
        headerLayout.Controls.Add(_lblCrd, 0, 1);
        headerLayout.Controls.Add(_lnkProfiles, 0, 2);
        headerLayout.Controls.Add(badgePanel, 0, 3);
        mainLayout.Controls.Add(headerLayout, 0, 0);

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
        _lstRegistrations.Columns.Add("Type", 60);
        _lstRegistrations.Columns.Add("State / Organization", -2);
        _lstRegistrations.Columns.Add("Category", 80);
        _lstRegistrations.Columns.Add("Status", 100);
        _lstRegistrations.Columns.Add("Date", 90);
        regPage.Controls.Add(_lstRegistrations);

        // Current Firm tab
        var firmPage = new TabPage("Current Firm");
        _firmDetailsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };
        firmPage.Controls.Add(_firmDetailsPanel);

        _tabs.TabPages.AddRange(new[] { empPage, discPage, qualPage, regPage, firmPage });
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
        _btnFindEmail = MakeActionButton("Find Email", Color.FromArgb(0, 120, 160));

        _btnRefresh.Click += (_, _) => { if (_advisor != null) RefreshRequested?.Invoke(this, _advisor); };
        _btnImportCrm.Click += (_, _) => { if (_advisor != null) ImportCrmRequested?.Invoke(this, _advisor); };
        _btnAddToList.Click += (_, _) => { if (_advisor != null) AddToListRequested?.Invoke(this, _advisor); };
        _btnFavorite.Click += (_, _) => { if (_advisor != null) FavoriteRequested?.Invoke(this, _advisor); };
        _btnExclude.Click += (_, _) => { if (_advisor != null) ExcludeRequested?.Invoke(this, _advisor); };
        _btnRestore.Click += (_, _) => { if (_advisor != null) RestoreRequested?.Invoke(this, _advisor); };
        _btnFindEmail.Click += (_, _) => { if (_advisor != null) FindEmailRequested?.Invoke(this, _advisor); };

        btnPanel.Controls.AddRange(new Control[] { _btnRefresh, _btnImportCrm, _btnAddToList, _btnFavorite, _btnExclude, _btnRestore, _btnFindEmail });
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
        _lblBcScopeBadge.Visible = false;
        _lblIaScopeBadge.Visible = false;
        _lnkProfiles.Visible = false;
        _lnkProfiles.Links.Clear();
        _btnRefresh.Enabled = false;
        _btnImportCrm.Enabled = false;
        _btnAddToList.Enabled = false;
        _btnFavorite.Enabled = false;
        _btnExclude.Enabled = false;
        _btnRestore.Enabled = false;
        _btnFindEmail.Enabled = false;
        _btnWatch.Enabled = false;
        _btnWatch.Text = "Watch";
        _firmDetailsPanel.Controls.Clear();
        _tabs.TabPages[4].Text = "Current Firm";
    }

    public void ShowAdvisor(Advisor advisor)
    {
        _advisor = advisor;

        _lblName.Text = advisor.FullName;
        _lblCrd.Text = $"CRD: {advisor.CrdNumber ?? "N/A"}"
                     + (advisor.IapdNumber != null ? $"  |  IAPD: {advisor.IapdNumber}" : "")
                     + (advisor.OtherNames != null ? $"  |  Also known as: {advisor.OtherNames}" : "")
                     + $"  |  Updated: {advisor.UpdatedAt:yyyy-MM-dd}";

        // Clickable profile links
        _lnkProfiles.Links.Clear();
        var linkParts = new List<string>();
        if (!string.IsNullOrEmpty(advisor.CrdNumber))
            linkParts.Add($"BrokerCheck|https://brokercheck.finra.org/individual/summary/{advisor.CrdNumber}");
        if (!string.IsNullOrEmpty(advisor.IapdNumber))
            linkParts.Add($"SEC IAPD|https://adviserinfo.sec.gov/individual/summary/{advisor.IapdNumber}");
        if (!string.IsNullOrEmpty(advisor.BrokerCheckReportPdfUrl))
            linkParts.Add($"📄 BrokerCheck PDF|{advisor.BrokerCheckReportPdfUrl}");
        if (linkParts.Count > 0)
        {
            var linkText = string.Join("   ", linkParts.Select(p => p.Split('|')[0]));
            _lnkProfiles.Text = linkText;
            int pos = 0;
            foreach (var part in linkParts)
            {
                var segments = part.Split('|');
                var label = segments[0];
                var url = segments[1];
                int idx = linkText.IndexOf(label, pos, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    _lnkProfiles.Links.Add(idx, label.Length, url);
                    pos = idx + label.Length;
                }
            }
            _lnkProfiles.Visible = true;
        }
        else
        {
            _lnkProfiles.Text = "";
            _lnkProfiles.Visible = false;
        }

        // Badges
        _lblSourceBadge.Text = advisor.Source ?? "";
        _lblSourceBadge.Visible = !string.IsNullOrEmpty(advisor.Source);

        _lblRecordTypeBadge.Text = advisor.RecordType ?? "";
        _lblRecordTypeBadge.Visible = !string.IsNullOrEmpty(advisor.RecordType);

        _lblStatusBadge.Text = advisor.RegistrationStatus ?? "";
        _lblStatusBadge.Visible = !string.IsNullOrEmpty(advisor.RegistrationStatus);

        _lblCrmBadge.Visible = advisor.IsImportedToCrm;
        _lblExcludedBadge.Visible = advisor.IsExcluded;

        if (!string.IsNullOrEmpty(advisor.BcScope))
        {
            _lblBcScopeBadge.Text = $"BC: {advisor.BcScope}";
            _lblBcScopeBadge.Visible = true;
        }
        else
        {
            _lblBcScopeBadge.Visible = false;
        }

        if (!string.IsNullOrEmpty(advisor.IaScope))
        {
            _lblIaScopeBadge.Text = $"IA: {advisor.IaScope}";
            _lblIaScopeBadge.Visible = true;
        }
        else
        {
            _lblIaScopeBadge.Visible = false;
        }

        // Info grid
        _infoGrid.Controls.Clear();
        _infoGrid.RowStyles.Clear();

        AddInfoRowWithLink("Firm:", advisor.CurrentFirmName ?? "—", advisor.CurrentFirmCrd,
                           crd => FirmNavigationRequested?.Invoke(this, crd),
                           "State:", advisor.State ?? "—");
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
        if (!string.IsNullOrEmpty(advisor.BcScope) || !string.IsNullOrEmpty(advisor.IaScope))
            AddInfoRow("BC Scope:", advisor.BcScope ?? "—", "IA Scope:", advisor.IaScope ?? "—");
        if (advisor.CareerStartDate.HasValue)
            AddInfoRow("Career Start:", advisor.CareerStartDate.Value.ToString("yyyy-MM-dd"),
                       "Total Firms:", advisor.TotalFirmCount.HasValue ? advisor.TotalFirmCount.Value.ToString() : "—");
        if (advisor.BcDisclosureCount > 0 || advisor.IaDisclosureCount > 0)
            AddInfoRow("BC Disclosures:", advisor.BcDisclosureCount.ToString(), "IA Disclosures:", advisor.IaDisclosureCount.ToString());

        var disclosureTypes = new List<string>();
        if (advisor.HasCriminalDisclosure) disclosureTypes.Add("Criminal");
        if (advisor.HasRegulatoryDisclosure) disclosureTypes.Add("Regulatory");
        if (advisor.HasCivilDisclosure) disclosureTypes.Add("Civil");
        if (advisor.HasCustomerComplaintDisclosure) disclosureTypes.Add("Customer Complaint");
        if (advisor.HasFinancialDisclosure) disclosureTypes.Add("Financial");
        if (advisor.HasTerminationDisclosure) disclosureTypes.Add("Termination");
        if (disclosureTypes.Count > 0)
            AddInfoRow("Disc. Types:", string.Join(", ", disclosureTypes), "", "");

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
            var location = !string.IsNullOrWhiteSpace(emp.FirmCity)
                ? $"{emp.FirmCity}{(!string.IsNullOrWhiteSpace(emp.FirmState) ? ", " + emp.FirmState : "")}"
                : (emp.Street ?? "");
            item.SubItems.Add(location);
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

        // Registrations
        _lstRegistrations.Items.Clear();
        if (advisor.Registrations.Count > 0)
        {
            // SRO registrations first, then state registrations sorted alphabetically
            var sroRegs = advisor.Registrations
                .Where(r => r.RegistrationType == "SRO")
                .OrderBy(r => r.SroName);
            var stateRegs = advisor.Registrations
                .Where(r => r.RegistrationType != "SRO")
                .OrderBy(r => r.StateCode);

            foreach (var reg in sroRegs.Concat(stateRegs))
            {
                bool isSro = reg.RegistrationType == "SRO";
                var item = new ListViewItem(isSro ? "SRO" : "State");
                item.SubItems.Add(isSro ? (reg.SroName ?? "") : reg.StateCode);
                item.SubItems.Add(reg.RegistrationCategory ?? "");
                item.SubItems.Add(reg.RegistrationStatus ?? "");
                item.SubItems.Add(reg.StatusDate ?? "");
                if (isSro) item.BackColor = Color.FromArgb(240, 248, 255);
                _lstRegistrations.Items.Add(item);
            }
        }
        else if (!string.IsNullOrEmpty(advisor.RegAuthorities))
        {
            // Fall back to RegAuthorities comma-joined state codes for older records
            foreach (var state in advisor.RegAuthorities.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var item = new ListViewItem("State");
                item.SubItems.Add(state);
                item.SubItems.Add("");
                item.SubItems.Add("");
                item.SubItems.Add("");
                _lstRegistrations.Items.Add(item);
            }
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
        // Show "Find Email" only when email is not yet known
        _btnFindEmail.Enabled = string.IsNullOrEmpty(advisor.Email);

        _btnWatch.Enabled = true;
        _btnWatch.Text = advisor.IsWatched ? "Watching ★" : "Watch";
        _btnWatch.BackColor = advisor.IsWatched
            ? Color.FromArgb(255, 200, 0)
            : Color.FromArgb(200, 190, 50);
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

    private void AddInfoRowWithLink(string label1, string linkText, string? linkData, Action<string>? onClick, string label2, string value2)
    {
        var lnk = new LinkLabel
        {
            Text = linkText,
            Font = new Font("Segoe UI", 8.5f),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(30, 30, 60),
            Padding = new Padding(2, 2, 0, 2)
        };

        if (!string.IsNullOrEmpty(linkData) && onClick != null)
        {
            lnk.Links.Add(0, linkText.Length, linkData);
            lnk.LinkClicked += (_, e) =>
            {
                if (e.Link?.LinkData is string data) onClick(data);
            };
        }
        else
        {
            lnk.LinkBehavior = LinkBehavior.NeverUnderline;
        }

        int row = _infoGrid.RowCount;
        _infoGrid.RowCount++;
        _infoGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _infoGrid.Controls.Add(MakeInfoLabel(label1), 0, row);
        _infoGrid.Controls.Add(lnk, 1, row);
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

    /// <summary>
    /// Populates the Current Firm tab with data from the advisor's current firm record.
    /// Call this after <see cref="ShowAdvisor"/> whenever the firm can be looked up.
    /// Pass null to clear the tab.
    /// </summary>
    public void SetFirm(Firm? firm)
    {
        _firmDetailsPanel.Controls.Clear();

        if (firm == null)
        {
            _tabs.TabPages[4].Text = "Current Firm";
            return;
        }

        _tabs.TabPages[4].Text = "Current Firm ✓";

        var layout = new TableLayoutPanel
        {
            ColumnCount = 4,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 8)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

        void AddFirmRow(string lbl1, string val1, string lbl2, string val2)
        {
            int row = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(MakeInfoLabel(lbl1), 0, row);
            layout.Controls.Add(MakeInfoValue(val1), 1, row);
            layout.Controls.Add(MakeInfoLabel(lbl2), 2, row);
            layout.Controls.Add(MakeInfoValue(val2), 3, row);
        }

        // Firm name as bold header
        var lblFirmName = new Label
        {
            Text = firm.Name,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            AutoSize = true,
            ForeColor = Color.FromArgb(30, 30, 80),
            Padding = new Padding(0, 0, 0, 8)
        };
        layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(lblFirmName, 0, 0);
        layout.SetColumnSpan(lblFirmName, 4);

        var location = !string.IsNullOrWhiteSpace(firm.City)
            ? $"{firm.City}{(!string.IsNullOrWhiteSpace(firm.State) ? ", " + firm.State : "")}"
            : (firm.State ?? "—");

        AddFirmRow("Firm CRD:", firm.CrdNumber ?? "—", "Status:", firm.RegistrationStatus ?? "—");
        AddFirmRow("City, State:", location, "Record Type:", firm.RecordType ?? "—");
        AddFirmRow("Reg. AUM:", FormatAum(firm.RegulatoryAum), "Advisors:", firm.NumberOfAdvisors.HasValue ? firm.NumberOfAdvisors.Value.ToString("N0") : "—");
        AddFirmRow("Broker Protocol:", firm.BrokerProtocolMember ? "Yes" : "No", "Business Type:", firm.BusinessType ?? "—");

        if (!string.IsNullOrWhiteSpace(firm.Website))
            AddFirmRow("Website:", firm.Website, "", "");

        _firmDetailsPanel.Controls.Add(layout);
    }

    private static string FormatAum(decimal? aum)
    {
        if (!aum.HasValue || aum.Value == 0) return "—";
        if (aum.Value >= 1_000_000_000m) return $"${aum.Value / 1_000_000_000m:F1}B";
        if (aum.Value >= 1_000_000m) return $"${aum.Value / 1_000_000m:F1}M";
        if (aum.Value >= 1_000m) return $"${aum.Value / 1_000m:F0}K";
        return $"${aum.Value:F0}";
    }
}
