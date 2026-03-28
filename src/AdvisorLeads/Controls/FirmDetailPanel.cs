using AdvisorLeads.Models;
using AdvisorLeads.Services;

namespace AdvisorLeads.Controls;

public class FirmDetailPanel : UserControl
{
    private Firm? _firm;

    // EDGAR services (set via SetServices)
    private AumAnalyticsService? _aumAnalytics;
    private ChangeDetectionService? _changeDetection;
    private FormAdvHistoricalService? _formAdvHistorical;
    private EdgarSubmissionsService? _edgarSubmissions;
    private EdgarSearchService? _edgarSearch;
    private MaTargetScoringService? _maScoring;

    private Label _lblName = null!;
    private Label _lblCrd = null!;
    private Label _lblSourceBadge = null!;
    private Label _lblTypeBadge = null!;
    private Label _lblStatusBadge = null!;
    private Label _lblBpBadge = null!;
    private TabControl _tabs = null!;
    private TableLayoutPanel _infoGrid = null!;
    private ListView _filingsListView = null!;
    private ListView _eventsListView = null!;
    private ListView _ownershipListView = null!;
    private TableLayoutPanel _aumGrid = null!;
    private TableLayoutPanel _scoreGrid = null!;

    public FirmDetailPanel()
    {
        BuildUI();
    }

    public void SetServices(
        AumAnalyticsService? aumAnalytics,
        ChangeDetectionService? changeDetection,
        FormAdvHistoricalService? formAdvHistorical,
        EdgarSubmissionsService? edgarSubmissions,
        EdgarSearchService? edgarSearch,
        MaTargetScoringService? maScoring)
    {
        _aumAnalytics = aumAnalytics;
        _changeDetection = changeDetection;
        _formAdvHistorical = formAdvHistorical;
        _edgarSubmissions = edgarSubmissions;
        _edgarSearch = edgarSearch;
        _maScoring = maScoring;
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
            RowCount = 2,
            Padding = new Padding(0)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // ── Header ──
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 0, 0, 12),
            Height = 80
        };

        _lblName = new Label
        {
            Text = "Select a firm",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = Color.FromArgb(30, 30, 80),
            Padding = new Padding(0, 0, 0, 4)
        };
        header.Controls.Add(_lblName);

        _lblCrd = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9),
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = Color.Gray,
            Padding = new Padding(2, 0, 0, 4)
        };
        header.Controls.Add(_lblCrd);

        var badgeFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0)
        };

        _lblSourceBadge = MakeBadge("SEC", Color.FromArgb(0, 120, 215));
        _lblTypeBadge = MakeBadge("Investment Advisor", Color.FromArgb(0, 150, 100));
        _lblStatusBadge = MakeBadge("", Color.Gray);
        _lblStatusBadge.Visible = false;
        _lblBpBadge = MakeBadge("Broker Protocol ✓", Color.FromArgb(0, 100, 170));
        _lblBpBadge.Visible = false;

        badgeFlow.Controls.AddRange(new Control[] { _lblSourceBadge, _lblTypeBadge, _lblStatusBadge, _lblBpBadge });
        header.Controls.Add(badgeFlow);

        mainLayout.Controls.Add(header, 0, 0);

        // ── Tabbed content ──
        _tabs = new TabControl { Dock = DockStyle.Fill };

        // Tab: Overview
        var tabOverview = new TabPage("Overview");
        _infoGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            AutoScroll = true,
            Padding = new Padding(0, 8, 0, 0)
        };
        _infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
        _infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        _infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
        _infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        tabOverview.Controls.Add(_infoGrid);

        // Tab: AUM & Growth
        var tabAum = new TabPage("AUM & Growth");
        _aumGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            AutoScroll = true,
            Padding = new Padding(8)
        };
        _aumGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        _aumGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        _aumGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        _aumGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        tabAum.Controls.Add(_aumGrid);

        // Tab: EDGAR Filings
        var tabFilings = new TabPage("EDGAR Filings");
        _filingsListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 8.5f),
            BorderStyle = BorderStyle.None
        };
        _filingsListView.Columns.Add("Date", 85);
        _filingsListView.Columns.Add("Type", 70);
        _filingsListView.Columns.Add("Description", -2);
        _filingsListView.Columns.Add("Accession #", 160);
        _filingsListView.DoubleClick += OnFilingDoubleClick;
        tabFilings.Controls.Add(_filingsListView);

        // Tab: Change Events
        var tabEvents = new TabPage("Change Events");
        _eventsListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 8.5f),
            BorderStyle = BorderStyle.None
        };
        _eventsListView.Columns.Add("Date", 85);
        _eventsListView.Columns.Add("Type", 110);
        _eventsListView.Columns.Add("Severity", 65);
        _eventsListView.Columns.Add("Description", -2);
        tabEvents.Controls.Add(_eventsListView);

        // Tab: Ownership
        var tabOwnership = new TabPage("Ownership");
        _ownershipListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 8.5f),
            BorderStyle = BorderStyle.None
        };
        _ownershipListView.Columns.Add("Name", -2);
        _ownershipListView.Columns.Add("Title", 140);
        _ownershipListView.Columns.Add("Ownership %", 90);
        _ownershipListView.Columns.Add("Type", 80);
        _ownershipListView.Columns.Add("Direct?", 55);
        _ownershipListView.Columns.Add("Filing Date", 85);
        tabOwnership.Controls.Add(_ownershipListView);

        // Tab: M&A Score
        var tabScore = new TabPage("M&A Score");
        _scoreGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            AutoScroll = true,
            Padding = new Padding(8)
        };
        _scoreGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _scoreGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _scoreGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _scoreGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        tabScore.Controls.Add(_scoreGrid);

        _tabs.TabPages.Add(tabOverview);
        _tabs.TabPages.Add(tabAum);
        _tabs.TabPages.Add(tabFilings);
        _tabs.TabPages.Add(tabEvents);
        _tabs.TabPages.Add(tabOwnership);
        _tabs.TabPages.Add(tabScore);

        mainLayout.Controls.Add(_tabs, 0, 1);
        outer.Controls.Add(mainLayout);
        this.Controls.Add(outer);
    }

    public void SetEmpty()
    {
        _firm = null;
        _lblName.Text = "Select a firm";
        _lblCrd.Text = "";
        _lblStatusBadge.Visible = false;
        _lblBpBadge.Visible = false;
        _infoGrid.Controls.Clear();
        _infoGrid.RowStyles.Clear();
        _aumGrid.Controls.Clear();
        _aumGrid.RowStyles.Clear();
        _filingsListView.Items.Clear();
        _eventsListView.Items.Clear();
        _ownershipListView.Items.Clear();
        _scoreGrid.Controls.Clear();
        _scoreGrid.RowStyles.Clear();
    }

    public void ShowFirm(Firm firm)
    {
        _firm = firm;
        _lblName.Text = firm.Name;
        _lblCrd.Text = $"CRD: {firm.CrdNumber}" + (string.IsNullOrEmpty(firm.SECNumber) ? "" : $"  |  SEC: {firm.SECNumber}");
        _lblTypeBadge.Text = firm.RecordType ?? "Investment Advisor";
        _lblSourceBadge.Text = firm.Source ?? "SEC";

        if (!string.IsNullOrEmpty(firm.RegistrationStatus))
        {
            _lblStatusBadge.Text = firm.RegistrationStatus;
            _lblStatusBadge.BackColor = firm.RegistrationStatus.Contains("APPROVED", StringComparison.OrdinalIgnoreCase)
                ? Color.FromArgb(0, 128, 0)
                : Color.Gray;
            _lblStatusBadge.Visible = true;
        }
        else
        {
            _lblStatusBadge.Visible = false;
        }

        _lblBpBadge.Visible = firm.BrokerProtocolMember;

        PopulateOverviewTab(firm);
        PopulateAumTab(firm);
        PopulateFilingsTab(firm);
        PopulateEventsTab(firm);
        PopulateOwnershipTab(firm);
        PopulateMaScoreTab(firm);
    }

    private void PopulateOverviewTab(Firm firm)
    {
        _infoGrid.Controls.Clear();
        _infoGrid.RowStyles.Clear();

        var rows = new List<(string, string, string, string)>();
        void AddRow(string l1, string v1, string l2 = "", string v2 = "")
            => rows.Add((l1, v1, l2, v2));

        AddRow("Legal Name:", firm.LegalName ?? "", "SEC Region:", firm.SECRegion ?? "");
        AddRow("Address:", firm.Address ?? "", "City:", firm.City ?? "");
        AddRow("State:", firm.State ?? "", "Zip:", firm.ZipCode ?? "");
        AddRow("Phone:", firm.Phone ?? "", "Fax:", firm.FaxPhone ?? "");
        AddRow("Mailing:", firm.MailingAddress ?? "", "Website:", firm.Website ?? "");
        AddRow("Org Type:", firm.BusinessType ?? "", "State of Org:", firm.StateOfOrganization ?? "");
        AddRow("IAR Reps:", firm.NumberOfAdvisors?.ToString("N0") ?? "",
               "Total Employees:", firm.NumberOfEmployees?.ToString("N0") ?? "");
        AddRow("Disc. RAUM:", firm.RegulatoryAum.HasValue ? FormatAumDisplay(firm.RegulatoryAum.Value) : firm.AumDescription ?? "",
               "Non-disc. RAUM:", firm.RegulatoryAumNonDiscretionary.HasValue ? FormatAumDisplay(firm.RegulatoryAumNonDiscretionary.Value) : "");
        AddRow("Clients:", firm.NumClients?.ToString("N0") ?? "",
               "Latest Filing:", firm.LatestFilingDate ?? "");
        AddRow("Reg. Date:", firm.RegistrationDate?.ToString("yyyy-MM-dd") ?? "", "Updated:", firm.UpdatedAt.ToString("yyyy-MM-dd"));

        // Compensation
        var comp = new List<string>();
        if (firm.CompensationFeeOnly == true) comp.Add("Fee-Only");
        if (firm.CompensationCommission == true) comp.Add("Commission");
        if (firm.CompensationHourly == true) comp.Add("Hourly");
        if (firm.CompensationPerformanceBased == true) comp.Add("Performance");
        if (comp.Count > 0)
            AddRow("Compensation:", string.Join(", ", comp), "Custody:", firm.HasCustody == true ? "Yes" : "No");
        else
            AddRow("Discretion:", firm.HasDiscretionaryAuthority == true ? "Yes" : "—",
                   "Custody:", firm.HasCustody == true ? "Yes" : "—");

        // Additional details
        if (firm.PrivateFundCount.HasValue)
            AddRow("Private Funds:", firm.PrivateFundCount.Value.ToString("N0"),
                   "Fund Assets:", firm.PrivateFundGrossAssets.HasValue ? FormatAumDisplay(firm.PrivateFundGrossAssets.Value) : "");

        if (firm.NumberOfOffices.HasValue)
            AddRow("Offices:", firm.NumberOfOffices.Value.ToString("N0"),
                   "Related AUM:", firm.TotalAumRelatedPersons.HasValue ? FormatAumDisplay(firm.TotalAumRelatedPersons.Value) : "—");

        // Business flags
        var flags = new List<string>();
        if (firm.IsBrokerDealer == true) flags.Add("Broker-Dealer");
        if (firm.IsInsuranceCompany == true) flags.Add("Insurance Co.");
        if (flags.Count > 0)
            AddRow("Also Registered:", string.Join(", ", flags), "", "");

        // Advisory activities
        if (!string.IsNullOrWhiteSpace(firm.AdvisoryActivities))
            AddRow("Advisory:", firm.AdvisoryActivities, "", "");

        // Client type breakdown
        var clientTypes = new List<string>();
        if (firm.ClientsIndividuals > 0) clientTypes.Add($"Individuals: {firm.ClientsIndividuals:N0}");
        if (firm.ClientsHighNetWorth > 0) clientTypes.Add($"HNW: {firm.ClientsHighNetWorth:N0}");
        if (firm.ClientsBankingInstitutions > 0) clientTypes.Add($"Banks: {firm.ClientsBankingInstitutions:N0}");
        if (firm.ClientsInvestmentCompanies > 0) clientTypes.Add($"Inv. Co.: {firm.ClientsInvestmentCompanies:N0}");
        if (firm.ClientsPensionPlans > 0) clientTypes.Add($"Pension: {firm.ClientsPensionPlans:N0}");
        if (firm.ClientsCharitable > 0) clientTypes.Add($"Charitable: {firm.ClientsCharitable:N0}");
        if (firm.ClientsGovernment > 0) clientTypes.Add($"Gov't: {firm.ClientsGovernment:N0}");
        if (firm.ClientsOther > 0) clientTypes.Add($"Other: {firm.ClientsOther:N0}");
        if (clientTypes.Count > 0)
        {
            var half = (clientTypes.Count + 1) / 2;
            AddRow("Client Mix:", string.Join(", ", clientTypes.Take(half)),
                   "", string.Join(", ", clientTypes.Skip(half)));
        }

        _infoGrid.RowCount = rows.Count;
        foreach (var (r, i) in rows.Select((r, i) => (r, i)))
        {
            _infoGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _infoGrid.Controls.Add(MakeLabel(r.Item1), 0, i);
            _infoGrid.Controls.Add(MakeValue(r.Item2), 1, i);
            _infoGrid.Controls.Add(MakeLabel(r.Item3), 2, i);
            _infoGrid.Controls.Add(MakeValue(r.Item4), 3, i);
        }
    }

    private void PopulateAumTab(Firm firm)
    {
        _aumGrid.Controls.Clear();
        _aumGrid.RowStyles.Clear();

        var rows = new List<(string, string, string, string)>();
        void AddRow(string l1, string v1, string l2 = "", string v2 = "")
            => rows.Add((l1, v1, l2, v2));

        var totalAum = (firm.RegulatoryAum ?? 0) + (firm.RegulatoryAumNonDiscretionary ?? 0);
        AddRow("Current AUM:", totalAum > 0 ? FormatAumDisplay(totalAum) : "—",
               "Discretionary:", firm.RegulatoryAum.HasValue ? FormatAumDisplay(firm.RegulatoryAum.Value) : "—");
        AddRow("Non-Discret.:", firm.RegulatoryAumNonDiscretionary.HasValue ? FormatAumDisplay(firm.RegulatoryAumNonDiscretionary.Value) : "—",
               "Related AUM:", firm.TotalAumRelatedPersons.HasValue ? FormatAumDisplay(firm.TotalAumRelatedPersons.Value) : "—");

        // Growth metrics from AumAnalyticsService
        if (_aumAnalytics != null)
        {
            var metrics = _aumAnalytics.CalculateGrowthMetrics(firm.CrdNumber);
            if (metrics != null)
            {
                if (metrics.AumGrowthYoY.HasValue)
                    AddRow("YoY Growth:", $"{metrics.AumGrowthYoY:+0.0;-0.0}%",
                           "Prior AUM:", metrics.AumOneYearAgo.HasValue ? FormatAumDisplay(metrics.AumOneYearAgo.Value) : "—");
                if (metrics.Cagr3Year.HasValue)
                    AddRow("3Y CAGR:", $"{metrics.Cagr3Year:+0.0;-0.0}%",
                           "5Y CAGR:", metrics.Cagr5Year.HasValue ? $"{metrics.Cagr5Year:+0.0;-0.0}%" : "—");
                if (metrics.ClientGrowthYoY.HasValue)
                    AddRow("Client Growth:", $"{metrics.ClientGrowthYoY:+0.0;-0.0}%",
                           "Emp. Growth:", metrics.EmployeeGrowthYoY.HasValue ? $"{metrics.EmployeeGrowthYoY:+0.0;-0.0}%" : "—");
                if (metrics.Trend != null)
                    AddRow("Trend:", metrics.Trend, "Snapshots:", metrics.SnapshotCount.ToString());
            }
            else
            {
                AddRow("Growth Data:", "No AUM history available yet", "", "");
            }
        }

        // AUM history timeline
        if (_aumAnalytics != null)
        {
            var history = _aumAnalytics.GetAumHistory(firm.CrdNumber);
            if (history.Count > 0)
            {
                AddRow("", "", "", "");
                AddRow("— AUM History —", "", "", "");
                foreach (var snap in history.TakeLast(12))
                {
                    var snapAum = snap.TotalAum ?? snap.RegulatoryAum ?? 0;
                    AddRow(snap.SnapshotDate.ToString("yyyy-MM") + ":",
                           snapAum > 0 ? FormatAumDisplay(snapAum) : "—",
                           "Employees:", snap.NumberOfEmployees?.ToString("N0") ?? "—");
                }
            }
        }

        _aumGrid.RowCount = rows.Count;
        foreach (var (r, i) in rows.Select((r, i) => (r, i)))
        {
            _aumGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _aumGrid.Controls.Add(MakeLabel(r.Item1), 0, i);
            _aumGrid.Controls.Add(MakeValue(r.Item2), 1, i);
            _aumGrid.Controls.Add(MakeLabel(r.Item3), 2, i);
            _aumGrid.Controls.Add(MakeValue(r.Item4), 3, i);
        }
    }

    private void PopulateFilingsTab(Firm firm)
    {
        _filingsListView.Items.Clear();
        if (_edgarSubmissions == null) return;

        var filings = _edgarSubmissions.GetFilings(firm.CrdNumber);
        if (filings.Count == 0)
        {
            var placeholder = new ListViewItem("No EDGAR filings loaded yet");
            placeholder.SubItems.Add("");
            placeholder.SubItems.Add("EDGAR data is fetched in the background");
            placeholder.SubItems.Add("");
            _filingsListView.Items.Add(placeholder);
            return;
        }

        _filingsListView.BeginUpdate();
        foreach (var filing in filings)
        {
            var item = new ListViewItem(filing.FilingDate.ToString("yyyy-MM-dd"));
            item.SubItems.Add(filing.FormType);
            item.SubItems.Add(filing.Description ?? "");
            item.SubItems.Add(filing.AccessionNumber);
            item.Tag = filing.FilingUrl;
            _filingsListView.Items.Add(item);
        }
        _filingsListView.EndUpdate();
    }

    private void PopulateEventsTab(Firm firm)
    {
        _eventsListView.Items.Clear();
        if (_changeDetection == null) return;

        var events = _changeDetection.GetEventsForFirm(firm.CrdNumber);
        if (events.Count == 0)
        {
            var placeholder = new ListViewItem("No change events detected yet");
            placeholder.SubItems.Add("");
            placeholder.SubItems.Add("");
            placeholder.SubItems.Add("Events are detected when monthly SEC data changes");
            _eventsListView.Items.Add(placeholder);
            return;
        }

        _eventsListView.BeginUpdate();
        foreach (var ev in events)
        {
            var item = new ListViewItem(ev.EventDate.ToString("yyyy-MM-dd"));
            item.SubItems.Add(ev.EventType);
            item.SubItems.Add(ev.Severity);
            item.SubItems.Add(ev.Description);

            item.ForeColor = ev.Severity switch
            {
                "HIGH" => Color.FromArgb(180, 0, 0),
                "MEDIUM" => Color.FromArgb(180, 120, 0),
                _ => Color.FromArgb(60, 60, 60)
            };
            _eventsListView.Items.Add(item);
        }
        _eventsListView.EndUpdate();
    }

    private void PopulateOwnershipTab(Firm firm)
    {
        _ownershipListView.Items.Clear();
        if (_formAdvHistorical == null) return;

        var owners = _formAdvHistorical.GetFirmOwnership(firm.CrdNumber);
        if (owners.Count == 0)
        {
            var placeholder = new ListViewItem("No ownership data loaded yet");
            placeholder.SubItems.Add("");
            placeholder.SubItems.Add("");
            placeholder.SubItems.Add("");
            placeholder.SubItems.Add("");
            placeholder.SubItems.Add("Import Form ADV historical data to populate");
            _ownershipListView.Items.Add(placeholder);
            return;
        }

        _ownershipListView.BeginUpdate();
        foreach (var owner in owners)
        {
            var item = new ListViewItem(owner.OwnerName);
            item.SubItems.Add(owner.Title ?? "");
            item.SubItems.Add(owner.OwnershipPercent.HasValue ? $"{owner.OwnershipPercent:F1}%" : "—");
            item.SubItems.Add(owner.EntityType ?? "");
            item.SubItems.Add(owner.IsDirectOwner ? "Yes" : "No");
            item.SubItems.Add(owner.FilingDate.ToString("yyyy-MM-dd"));
            _ownershipListView.Items.Add(item);
        }
        _ownershipListView.EndUpdate();
    }

    private void PopulateMaScoreTab(Firm firm)
    {
        _scoreGrid.Controls.Clear();
        _scoreGrid.RowStyles.Clear();

        if (_maScoring == null)
        {
            AddScoreRow("M&A Scoring:", "Service not available", "", "");
            FinishScoreGrid();
            return;
        }

        FirmMaScore? score;
        try { score = _maScoring.ScoreFirm(firm.CrdNumber); }
        catch { score = null; }

        if (score == null)
        {
            AddScoreRow("M&A Score:", "Insufficient data for scoring", "", "");
            FinishScoreGrid();
            return;
        }

        // Header with grade badge
        AddScoreRow("Total Score:", $"{score.TotalScore}/100  ({score.Grade})",
                     "Trend:", score.Trend ?? "—");
        AddScoreRow("Raw Points:", $"{score.RawPoints:F0} / {score.MaxPoints:F0}", "", "");

        // Category breakdown
        AddScoreRow("", "", "", "");
        AddScoreRow("— Score Breakdown —", "", "", "");
        AddScoreRow("AUM Growth:", $"{score.AumGrowthPoints:F0} pts",
                     "Detail:", score.AumGrowthDetail ?? "—");
        AddScoreRow("Firm Size:", $"{score.SizePoints:F0} pts",
                     "Detail:", score.SizeDetail ?? "—");
        AddScoreRow("Broker Protocol:", $"{score.ProtocolPoints:F0} pts",
                     "Detail:", score.ProtocolDetail ?? "—");
        AddScoreRow("Compliance:", $"{score.CompliancePoints:F0} pts",
                     "Detail:", score.ComplianceDetail ?? "—");
        AddScoreRow("Ownership:", $"{score.OwnershipPoints:F0} pts",
                     "Detail:", score.OwnershipDetail ?? "—");
        AddScoreRow("Filing Activity:", $"{score.FilingPoints:F0} pts",
                     "Detail:", score.FilingDetail ?? "—");
        AddScoreRow("Change Events:", $"{score.EventPoints:F0} pts",
                     "Detail:", score.EventDetail ?? "—");
        AddScoreRow("EDGAR Search:", $"{score.SearchPoints:F0} pts",
                     "Detail:", score.SearchDetail ?? "—");

        FinishScoreGrid();
    }

    private readonly List<(string, string, string, string)> _scoreRows = new();

    private void AddScoreRow(string l1, string v1, string l2, string v2)
        => _scoreRows.Add((l1, v1, l2, v2));

    private void FinishScoreGrid()
    {
        _scoreGrid.RowCount = _scoreRows.Count;
        foreach (var (r, i) in _scoreRows.Select((r, i) => (r, i)))
        {
            _scoreGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _scoreGrid.Controls.Add(MakeLabel(r.Item1), 0, i);
            _scoreGrid.Controls.Add(MakeValue(r.Item2), 1, i);
            _scoreGrid.Controls.Add(MakeLabel(r.Item3), 2, i);
            _scoreGrid.Controls.Add(MakeValue(r.Item4), 3, i);
        }
        _scoreRows.Clear();
    }

    private void OnFilingDoubleClick(object? sender, EventArgs e)
    {
        if (_filingsListView.SelectedItems.Count == 0) return;
        var url = _filingsListView.SelectedItems[0].Tag as string;
        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { /* ignore browser launch failures */ }
        }
    }

    private static Label MakeBadge(string text, Color back)
    {
        return new Label
        {
            Text = text,
            BackColor = back,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2),
            Margin = new Padding(0, 0, 4, 0)
        };
    }

    private static Label MakeLabel(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(80, 80, 80),
            AutoSize = true,
            Padding = new Padding(0, 4, 8, 4),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label MakeValue(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(20, 20, 20),
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 4),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static string FormatAumDisplay(decimal aum)
    {
        if (aum >= 1_000_000_000) return $"${aum / 1_000_000_000:F1}B";
        if (aum >= 1_000_000)     return $"${aum / 1_000_000:F1}M";
        if (aum >= 1_000)         return $"${aum / 1_000:F0}K";
        return $"${aum:F0}";
    }
}
