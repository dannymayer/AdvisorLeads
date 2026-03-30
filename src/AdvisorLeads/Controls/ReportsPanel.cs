using System.Text;
using AdvisorLeads.Models;
using AdvisorLeads.Services;

namespace AdvisorLeads.Controls;

public class ReportsPanel : UserControl
{
    private static readonly string[] States =
    {
        "", "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA", "HI", "ID",
        "IL", "IN", "IA", "KS", "KY", "LA", "ME", "MD", "MA", "MI", "MN", "MS", "MO",
        "MT", "NE", "NV", "NH", "NJ", "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA",
        "RI", "SC", "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY", "DC"
    };

    private sealed record TabPageControls(
        ListBox ReportList,
        DataGridView Grid,
        Panel FilterState,
        Panel FilterRecordType,
        Panel FilterMinAum,
        Panel FilterMinAdvisors,
        Panel FilterMinYearsExp,
        Panel FilterMinTotalFirmCount,
        Panel FilterMinHnwPct,
        Panel FilterMinStateRegCount,
        Panel FilterCompType,
        CheckBox ActiveOnlyCheck,
        CheckBox NoDisclosuresCheck,
        CheckBox BrokerProtocolCheck,
        CheckBox FavoritedOnlyCheck,
        Button RunButton,
        ProgressBar Progress,
        Label RowCountLabel,
        Label ErrorLabel,
        Button ExportCsvButton,
        ComboBox StateCombo,
        ComboBox RecordTypeCombo,
        TextBox MinAumText,
        TextBox MinAdvisorsText,
        TextBox MinYearsExpText,
        TextBox MinTotalFirmCountText,
        TextBox MinHnwClientPctText,
        TextBox MinStateRegCountText,
        ComboBox CompTypeCombo,
        Panel DescPanel,
        Label LastRunLabel);

    private readonly record struct ReportDesc(string Name, string Description, string UseCase, string[] KeyMetrics);

    private static readonly Dictionary<string, ReportDesc> _reportDescriptions = new()
    {
        ["R1"]  = new("Flight Risk Scorecard",
            "Scores each advisor on their likelihood of switching firms based on tenure, employment history, and AUM trends.",
            "Identify advisors who may be ready to move — prioritize outreach to high-score targets.",
            new[] { "Tenure", "Firm change count", "AUM trajectory", "Composite risk score (0–100)" }),
        ["R2"]  = new("High-Value Target",
            "Ranks advisors by a composite score factoring in experience, qualifications, firm AUM, and productivity.",
            "Surface the highest-value prospects for your recruiting pipeline.",
            new[] { "Years of experience", "Qualifications count", "AUM/advisor", "Target score (0–100)" }),
        ["R3"]  = new("Tenure Distribution",
            "Summarizes how long advisors have been at their current firm, bucketed into tenure ranges.",
            "Understand market tenure patterns and identify the largest pools of potentially mobile advisors.",
            new[] { "Tenure bucket", "Advisor count", "Average AUM/advisor", "Disclosure rate" }),
        ["R4"]  = new("Serial Mover Profile",
            "Identifies advisors with a history of frequent firm changes, including predicted readiness for their next move.",
            "Target advisors with proven mobility — they are statistically more likely to move again.",
            new[] { "Firm change count", "Change rate (moves/year)", "\"Due for Move\" flag" }),
        ["R5"]  = new("New Market Entrants",
            "Lists advisors who recently started their careers, useful for early relationship-building.",
            "Build relationships with new advisors before competitors do.",
            new[] { "Career start date", "Onboarding quarter", "Firm metrics" }),
        ["R6"]  = new("Firm Headcount Trend",
            "Shows firms gaining or losing advisors based on historical headcount data.",
            "Identify firms in decline (talent leaving) or growth (healthy environment) for strategic targeting.",
            new[] { "Current vs. prior headcount", "Change percentage", "AUM", "Pipeline count" }),
        ["R7"]  = new("Broker Protocol Directory",
            "Lists all firms enrolled in the Broker Protocol, which eases advisor transitions.",
            "Target protocol firms when recruiting — advisors can bring their book without legal risk.",
            new[] { "Protocol enrollment", "AUM/advisor", "HNW client percentage", "Comp model" }),
        ["R8"]  = new("Firm AUM Trajectory",
            "Tracks AUM changes across 1, 3, and 5-year windows for each firm.",
            "Find firms on a downward AUM trajectory — advisors there may be more receptive to a move.",
            new[] { "AUM at 1yr/3yr/5yr ago", "Percentage change over each period" }),
        ["R9"]  = new("Competitive Landscape",
            "Shows each firm's share of advisors and AUM within the filtered market.",
            "Understand which firms dominate the market and benchmark your targets.",
            new[] { "Advisor market share (%)", "AUM market share (%)" }),
        ["R10"] = new("Credential Frequency",
            "Shows how common each professional credential (CFP, CFA, etc.) is across the advisor population.",
            "Understand credential saturation to target advisors with specific designations.",
            new[] { "Credential name", "Advisor count", "Percentage of market" }),
        ["R11"] = new("Geographic Density",
            "Summarizes advisor distribution, activity, and experience by state.",
            "Identify high-density states for regional campaigns or underserved markets for expansion.",
            new[] { "Advisor count", "Active count", "Average experience", "Disclosure rate" }),
        ["R12"] = new("AUM by Geography",
            "Shows average AUM per advisor and concentration of large-AUM firms by state.",
            "Target high-AUM states for premium recruiting campaigns.",
            new[] { "Average AUM/advisor", "Count of advisors above $500M AUM threshold" }),
        ["R13"] = new("Disclosure Risk Profile",
            "Details the disclosure history of advisors, categorized by disclosure type.",
            "Compliance screening — identify advisors with clean vs. problematic regulatory histories.",
            new[] { "Disclosure count by type (criminal, regulatory, civil, complaints, financial, termination)" }),
        ["R14"] = new("Clean Record Premium",
            "Lists advisors with no disclosure history who are also Broker Protocol members.",
            "Premium recruits — advisors with spotless compliance records and easy transition eligibility.",
            new[] { "Tenure", "Experience", "Email availability", "AUM/advisor" }),
        ["R15"] = new("Pipeline Funnel",
            "Shows your recruiting funnel from all active advisors down to those already in your CRM.",
            "Track pipeline health and identify where prospects are dropping off.",
            new[] { "Active count", "Favorited count", "With email", "In CRM (funnel stages)" }),
        ["R16"] = new("Contact Coverage Gap",
            "Lists advisors missing direct email but whose firm has contactable information.",
            "Prioritize advisors you can reach indirectly through firm contact info.",
            new[] { "Missing email flag", "Firm phone", "Firm website" }),
        ["R17"] = new("Firm Stability Signal",
            "Scores firms on instability based on AUM changes, headcount shifts, and disclosure rates.",
            "Target advisors at unstable firms — they may be more open to conversations.",
            new[] { "AUM change", "Headcount change", "Average disclosures", "Instability score (0–100)" }),
        ["R18"] = new("Compensation Analysis",
            "Breaks down the market by compensation model (fee-only, commission, both).",
            "Align your recruiting pitch to the compensation culture at target firms.",
            new[] { "Firm count", "Advisor count", "Average AUM", "HNW percentage by comp model" }),
        ["R19"] = new("HNW Focus Firms",
            "Ranks firms by their focus on high-net-worth clients, with upmarket scoring.",
            "Target firms already serving HNW clients — advisors there expect premium compensation offers.",
            new[] { "HNW client percentage", "Fee-only indicator", "Private fund count", "Upmarket score" }),
        ["R20"] = new("Multi-State Registration",
            "Identifies advisors registered in multiple states, indicating geographic portability.",
            "Find advisors who can move to your target markets without re-registration hurdles.",
            new[] { "Registered state count", "States list", "Portability tier (1/2/3)" }),
    };

    private static readonly Dictionary<string, string> _columnTooltips = new()
    {
        ["AUM"]          = "Total regulatory AUM reported by the firm on their most recent Form ADV filing.",
        ["AUM/Adv"]      = "Firm's regulatory AUM divided by the number of registered advisors — a productivity indicator.",
        ["AUM∆1yr%"]     = "Percentage change in firm AUM compared to one year ago.",
        ["Change Rate"]  = "Average number of employer changes per year of career.",
        ["Tier"]         = "Portability tier: 1 = 2–4 states, 2 = 5–9 states, 3 = 10+ states.",
        ["BP"]           = "Broker Protocol member. Advisors at protocol firms can transition without legal risk to their book of business.",
        ["HNW%"]         = "Percentage of the firm's clients classified as high-net-worth ($1M+ investable assets).",
        ["Disc Rate%"]   = "Percentage of advisors at this firm who have at least one regulatory disclosure.",
        ["Due for Move"] = "Estimated readiness based on historical change rate and current tenure.",
        ["Pipeline"]     = "Number of advisors from this firm in your favorites list.",
    };

    private readonly ReportingService _svc;
    private readonly TabPageControls?[] _tabControls = new TabPageControls?[6];

    public ReportsPanel(ReportingService svc)
    {
        _svc = svc;
        Dock = DockStyle.Fill;
        BuildUI();
    }

    private void BuildUI()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };

        string[][] tabReports =
        {
            new[] { "R1: Flight Risk Scorecard", "R2: High-Value Target", "R3: Tenure Distribution", "R4: Serial Mover", "R5: New Market Entrants" },
            new[] { "R6: Headcount Trend", "R7: Broker Protocol Directory", "R8: AUM Trajectory", "R9: Competitive Landscape" },
            new[] { "R10: Credential Frequency", "R11: Geographic Density", "R12: AUM by Geography", "R20: Multi-State Registration" },
            new[] { "R13: Disclosure Risk Profile", "R14: Clean Record Premium", "R17: Firm Stability Signal" },
            new[] { "R15: Pipeline Funnel", "R16: Contact Coverage Gap" },
            new[] { "R18: Compensation Analysis", "R19: HNW Focus Firms" }
        };

        string[] tabNames = { "Advisor Pipeline", "Firm Intelligence", "Market Analysis", "Compliance & Risk", "My Pipeline", "Comp & Structure" };

        for (int i = 0; i < 6; i++)
        {
            var tp = new TabPage(tabNames[i]) { Padding = new Padding(0) };
            _tabControls[i] = CreateTabPage(tp, tabReports[i], i);
            tabs.TabPages.Add(tp);
        }

        Controls.Add(tabs);
        _tabControls[0]!.ReportList.SelectedIndex = 0;
    }

    private TabPageControls CreateTabPage(TabPage tabPage, string[] reports, int tabIndex)
    {
        var listBox = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 8.5f)
        };

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
            BorderStyle = BorderStyle.None,
            BackgroundColor = Color.White,
            RowHeadersVisible = false
        };

        var cboState = new ComboBox { Width = 55, DropDownStyle = ComboBoxStyle.DropDownList };
        cboState.Items.AddRange(States);

        var cboRecordType = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        cboRecordType.Items.AddRange(new object[] { "", "Investment Advisor Representative", "Registered Representative" });

        var txtMinAum = new TextBox { Width = 70 };
        var txtMinAdvisors = new TextBox { Width = 50 };
        var txtMinYearsExp = new TextBox { Width = 40 };
        var txtMinTotalFirmCount = new TextBox { Width = 40 };
        var txtMinHnwPct = new TextBox { Width = 40 };
        var txtMinStateRegCount = new TextBox { Width = 40 };

        var cboCompType = new ComboBox { Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
        cboCompType.Items.AddRange(new object[] { "", "FeeOnly", "Commission", "Both" });

        var chkActiveOnly = new CheckBox { Text = "Active Only", Checked = true, AutoSize = true };
        var chkNoDisclosures = new CheckBox { Text = "No Disclosures", AutoSize = true };
        var chkBrokerProtocol = new CheckBox { Text = "BP Only", AutoSize = true };
        var chkFavorited = new CheckBox { Text = "Favorited Only", AutoSize = true };

        var btnRun = new Button
        {
            Text = "▶ Run Report",
            BackColor = Color.FromArgb(0, 100, 200),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Height = 28,
            AutoSize = true
        };
        btnRun.FlatAppearance.BorderSize = 0;

        var pb = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            Width = 120,
            Height = 20,
            Visible = false
        };

        var pnlState = FilterPair("State:", cboState);
        var pnlRecordType = FilterPair("Type:", cboRecordType);
        var pnlMinAum = FilterPair("Min AUM ($M):", txtMinAum);
        var pnlMinAdvisors = FilterPair("Min Advisors:", txtMinAdvisors);
        var pnlMinYearsExp = FilterPair("Min Exp (yrs):", txtMinYearsExp);
        var pnlMinTotalFirmCount = FilterPair("Min Firm Count:", txtMinTotalFirmCount);
        var pnlMinHnwPct = FilterPair("Min HNW %:", txtMinHnwPct);
        var pnlMinStateRegCount = FilterPair("Min State Regs:", txtMinStateRegCount);
        var pnlCompType = FilterPair("Comp Type:", cboCompType);

        var rtbDesc = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.ControlLight,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Font = new Font("Segoe UI", 9f)
        };
        var descPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 120,
            BackColor = SystemColors.ControlLight,
            Padding = new Padding(8, 6, 8, 4)
        };
        descPanel.Controls.Add(rtbDesc);
        descPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(SystemColors.ControlDark, 1);
            e.Graphics.DrawLine(pen, 0, descPanel.Height - 1, descPanel.Width, descPanel.Height - 1);
        };

        var btnExport = new Button
        {
            Text = "Export CSV",
            Height = 26,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat
        };

        var lblRowCount = new Label
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(8, 3, 0, 0)
        };

        var lblError = new Label
        {
            AutoSize = true,
            ForeColor = Color.Red,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(8, 3, 0, 0)
        };

        var lblLastRun = new Label
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(12, 3, 0, 0),
            ForeColor = SystemColors.GrayText
        };

        var filterFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(4, 4, 4, 4)
        };
        filterFlow.Controls.Add(pnlState);
        filterFlow.Controls.Add(pnlRecordType);
        filterFlow.Controls.Add(pnlMinAum);
        filterFlow.Controls.Add(pnlMinAdvisors);
        filterFlow.Controls.Add(pnlMinYearsExp);
        filterFlow.Controls.Add(pnlMinTotalFirmCount);
        filterFlow.Controls.Add(pnlMinHnwPct);
        filterFlow.Controls.Add(pnlMinStateRegCount);
        filterFlow.Controls.Add(pnlCompType);
        filterFlow.Controls.Add(chkActiveOnly);
        filterFlow.Controls.Add(chkNoDisclosures);
        filterFlow.Controls.Add(chkBrokerProtocol);
        filterFlow.Controls.Add(chkFavorited);
        filterFlow.Controls.Add(btnRun);
        filterFlow.Controls.Add(pb);

        var bottomBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(4, 2, 4, 2)
        };
        bottomBar.Controls.Add(btnExport);
        bottomBar.Controls.Add(lblRowCount);
        bottomBar.Controls.Add(lblError);
        bottomBar.Controls.Add(lblLastRun);

        // WinForms docks in reverse Controls-collection order (highest index first).
        // Add grid first (Fill) → index 0; add descPanel last → highest index, claims top edge first.
        var rightPanel = new Panel { Dock = DockStyle.Fill };
        rightPanel.Controls.Add(grid);
        rightPanel.Controls.Add(bottomBar);
        rightPanel.Controls.Add(filterFlow);
        rightPanel.Controls.Add(descPanel);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel1,
            Panel1MinSize = 120
        };
        try { split.SplitterDistance = 160; } catch { /* not yet laid out */ }

        listBox.Items.AddRange(reports);
        listBox.Dock = DockStyle.Fill;
        split.Panel1.Controls.Add(listBox);
        split.Panel2.Controls.Add(rightPanel);
        tabPage.Controls.Add(split);

        var tc = new TabPageControls(
            listBox, grid,
            pnlState, pnlRecordType, pnlMinAum, pnlMinAdvisors, pnlMinYearsExp,
            pnlMinTotalFirmCount, pnlMinHnwPct, pnlMinStateRegCount, pnlCompType,
            chkActiveOnly, chkNoDisclosures, chkBrokerProtocol, chkFavorited,
            btnRun, pb,
            lblRowCount, lblError, btnExport,
            cboState, cboRecordType, txtMinAum, txtMinAdvisors, txtMinYearsExp,
            txtMinTotalFirmCount, txtMinHnwPct, txtMinStateRegCount, cboCompType,
            descPanel, lblLastRun);

        listBox.SelectedIndexChanged += (_, _) => UpdateFilterVisibility(tabIndex);
        btnRun.Click += (_, _) => RunReportAsync(tabIndex);
        btnExport.Click += (_, _) =>
        {
            try
            {
                using var dlg = new SaveFileDialog
                {
                    Filter = "CSV files|*.csv",
                    DefaultExt = "csv",
                    FileName = "report.csv"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;
                var csv = GenerateCsvContent(tabIndex);
                using var sw = new StreamWriter(dlg.FileName, false, Encoding.UTF8);
                sw.Write(csv);
            }
            catch (Exception ex)
            {
                tc.ErrorLabel.Text = $"Export error: {ex.Message}";
            }
        };

        return tc;
    }

    private static Panel FilterPair(string label, Control control)
    {
        var p = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 2, 6, 2)
        };
        p.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Margin = new Padding(0, 3, 2, 0),
            TextAlign = ContentAlignment.MiddleLeft
        });
        p.Controls.Add(control);
        return p;
    }

    private void UpdateFilterVisibility(int tabIndex)
    {
        var tc = _tabControls[tabIndex]!;

        tc.FilterState.Visible = false;
        tc.FilterRecordType.Visible = false;
        tc.FilterMinAum.Visible = false;
        tc.FilterMinAdvisors.Visible = false;
        tc.FilterMinYearsExp.Visible = false;
        tc.FilterMinTotalFirmCount.Visible = false;
        tc.FilterMinHnwPct.Visible = false;
        tc.FilterMinStateRegCount.Visible = false;
        tc.FilterCompType.Visible = false;
        tc.NoDisclosuresCheck.Visible = false;
        tc.BrokerProtocolCheck.Visible = false;
        tc.FavoritedOnlyCheck.Visible = false;

        string report = tc.ReportList.SelectedItem?.ToString() ?? "";
        if (string.IsNullOrEmpty(report)) return;

        if (IsAdvisorReport(report))
        {
            tc.FilterState.Visible = true;
            tc.FilterRecordType.Visible = true;
            tc.FilterMinYearsExp.Visible = true;
            tc.NoDisclosuresCheck.Visible = true;
        }
        else if (IsFirmReport(report))
        {
            tc.FilterState.Visible = true;
            tc.FilterMinAdvisors.Visible = true;
            tc.FilterMinAum.Visible = true;
        }

        if (report.StartsWith("R4:")) tc.FilterMinTotalFirmCount.Visible = true;
        if (report.StartsWith("R7:")) tc.BrokerProtocolCheck.Visible = true;
        if (report.StartsWith("R18:")) tc.FilterCompType.Visible = true;
        if (report.StartsWith("R19:")) tc.FilterMinHnwPct.Visible = true;
        if (report.StartsWith("R15:") || report.StartsWith("R16:")) tc.FavoritedOnlyCheck.Visible = true;
        if (report.StartsWith("R20:")) tc.FilterMinStateRegCount.Visible = true;

        UpdateDescriptionPanel(tc.DescPanel, report);
    }

    private static bool IsAdvisorReport(string report) =>
        report.StartsWith("R1:") || report.StartsWith("R2:") || report.StartsWith("R3:") ||
        report.StartsWith("R4:") || report.StartsWith("R5:") || report.StartsWith("R10:") ||
        report.StartsWith("R11:") || report.StartsWith("R12:") || report.StartsWith("R13:") ||
        report.StartsWith("R14:") || report.StartsWith("R15:") || report.StartsWith("R16:") ||
        report.StartsWith("R20:");

    private static bool IsFirmReport(string report) =>
        report.StartsWith("R6:") || report.StartsWith("R7:") || report.StartsWith("R8:") ||
        report.StartsWith("R9:") || report.StartsWith("R17:") || report.StartsWith("R18:") ||
        report.StartsWith("R19:");

    private async void RunReportAsync(int tabIndex)
    {
        var tc = _tabControls[tabIndex]!;
        string report = tc.ReportList.SelectedItem?.ToString() ?? "";
        if (string.IsNullOrEmpty(report)) return;

        tc.RunButton.Enabled = false;
        tc.Progress.Visible = true;
        tc.Grid.Rows.Clear();
        tc.Grid.Columns.Clear();
        tc.ErrorLabel.ForeColor = Color.Red;
        tc.ErrorLabel.Text = "";
        tc.RowCountLabel.Text = "";

        try
        {
            var filter = BuildFilter(tc);
            await RunReportCoreAsync(report, filter, tc);
            tc.LastRunLabel.Text = $"Last run: {DateTime.Now:h:mm tt}";
        }
        catch (Exception ex)
        {
            tc.ErrorLabel.Text = $"Error: {ex.Message}";
        }
        finally
        {
            tc.RunButton.Enabled = true;
            tc.Progress.Visible = false;
        }
    }

    private async Task RunReportCoreAsync(string report, ReportFilter filter, TabPageControls tc)
    {
        var g = tc.Grid;

        if (report.StartsWith("R1:"))
        {
            var data = await _svc.GetFlightRiskAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Name"), TC("CRD"), TC("State"), TC("Firm"),
                TC("Tenure(yrs)", true), TC("Exp(yrs)", true), TC("FirmChanges", true),
                TC("ChangeRate", true), TC("AUM", true), TC("AUM/Adv", true),
                TC("AUM∆1yr%", true), TC("BP"), TC("Disclosures"), TC("Score", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.FullName, r.CrdNumber, r.State, r.CurrentFirmName,
                    r.TenureAtCurrentFirmYears, r.YearsOfExperience, r.TotalFirmCount,
                    FormatRate(r.FirmChangeRate), FormatAum(r.FirmRegulatoryAum),
                    FormatAum(r.AumPerAdvisor), FormatPct(r.FirmAumChange1YrPct),
                    r.BrokerProtocolMember ? "✓" : "", r.HasDisclosures ? "✓" : "",
                    r.FlightRiskScore);
            tc.RowCountLabel.Text = $"{data.Count:N0} records";
        }
        else if (report.StartsWith("R2:"))
        {
            var data = await _svc.GetHighValueTargetsAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Name"), TC("CRD"), TC("State"), TC("Firm"), TC("Exp(yrs)", true),
                TC("Qualifications"), TC("Firm AUM", true), TC("#Advisors", true),
                TC("BP"), TC("AUM/Adv", true), TC("Score", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.FullName, r.CrdNumber, r.State, r.CurrentFirmName,
                    r.YearsOfExperience, r.Qualifications, FormatAum(r.FirmRegulatoryAum),
                    r.NumberOfAdvisors, r.BrokerProtocolMember ? "✓" : "",
                    FormatAum(r.AumPerAdvisor), r.TargetScore);
            tc.RowCountLabel.Text = $"{data.Count:N0} records";
        }
        else if (report.StartsWith("R3:"))
        {
            var data = await _svc.GetTenureDistributionSummaryAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Bucket"), TC("Advisors", true), TC("Avg AUM/Adv", true), TC("Disc Rate%", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.TenureBucket, r.AdvisorCount,
                    FormatAum(r.AvgAumPerAdvisor), FormatPct(r.DisclosureRate));
            tc.RowCountLabel.Text = $"{data.Count:N0} buckets";
        }
        else if (report.StartsWith("R4:"))
        {
            var data = await _svc.GetSerialMoversAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Name"), TC("CRD"), TC("State"), TC("Firm"), TC("#Firms", true),
                TC("Exp(yrs)", true), TC("Change Rate", true), TC("Tenure(yrs)", true),
                TC("Firm AUM", true), TC("AUM/Adv", true), TC("Due for Move")
            });
            foreach (var r in data)
                g.Rows.Add(r.FullName, r.CrdNumber, r.State, r.CurrentFirmName,
                    r.TotalFirmCount, r.YearsOfExperience, FormatRate(r.FirmChangeRate),
                    r.TenureAtCurrentFirmYears, FormatAum(r.FirmRegulatoryAum),
                    FormatAum(r.AumPerAdvisor), r.DueForMove ? "✓" : "");
            tc.RowCountLabel.Text = $"{data.Count:N0} records";
        }
        else if (report.StartsWith("R5:"))
        {
            var data = await _svc.GetNewMarketEntrantsAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Name"), TC("CRD"), TC("State"), TC("Firm"), TC("Career Start"),
                TC("Quarter"), TC("Exp(yrs)", true), TC("Email"),
                TC("Disclosures"), TC("Firm AUM", true), TC("#Advisors", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.FullName, r.CrdNumber, r.State, r.CurrentFirmName,
                    r.CareerStartDate?.ToString("yyyy-MM-dd") ?? "", r.CareerQuarter,
                    r.YearsOfExperience, r.Email, r.HasDisclosures ? "✓" : "",
                    FormatAum(r.FirmRegulatoryAum), r.NumberOfAdvisors);
            tc.RowCountLabel.Text = $"{data.Count:N0} records";
        }
        else if (report.StartsWith("R6:"))
        {
            var data = await _svc.GetFirmHeadcountTrendAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Firm"), TC("CRD"), TC("State"), TC("Advisors", true), TC("Prior Count", true),
                TC("Change", true), TC("Change%", true), TC("AUM", true),
                TC("Last Filing"), TC("Pipeline", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.Name, r.CrdNumber, r.State, r.NumberOfAdvisors,
                    r.PriorAdvisorCount, r.AdvisorCountChange,
                    FormatPct(r.AdvisorCountChangePct), FormatAum(r.RegulatoryAum),
                    r.LatestFilingDate, r.PipelineCount);
            tc.RowCountLabel.Text = $"{data.Count:N0} records";
        }
        else if (report.StartsWith("R7:"))
        {
            var data = await _svc.GetBrokerProtocolDirectoryAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Firm"), TC("CRD"), TC("State"), TC("City"), TC("Phone"), TC("Website"),
                TC("Advisors", true), TC("AUM", true), TC("AUM/Adv", true),
                TC("HNW%", true), TC("Comp Model"), TC("Favorited", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.Name, r.CrdNumber, r.State, r.City, r.Phone, r.Website,
                    r.NumberOfAdvisors, FormatAum(r.RegulatoryAum), FormatAum(r.AumPerAdvisor),
                    FormatPct(r.HnwClientPct), r.CompensationModel, r.FavoritedAdvisorCount);
            tc.RowCountLabel.Text = $"{data.Count:N0} records";
        }
        else if (report.StartsWith("R8:"))
        {
            var data = await _svc.GetFirmAumTrajectoryAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Firm"), TC("CRD"), TC("State"), TC("AUM", true), TC("Advisors", true),
                TC("1yr Ago", true), TC("3yr Ago", true), TC("5yr Ago", true),
                TC("∆1yr%", true), TC("∆3yr%", true), TC("∆5yr%", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.Name, r.CrdNumber, r.State,
                    FormatAum(r.CurrentAum), r.NumberOfAdvisors,
                    FormatAum(r.Aum1YrAgo), FormatAum(r.Aum3YrAgo), FormatAum(r.Aum5YrAgo),
                    FormatPct(r.AumChange1YrPct), FormatPct(r.AumChange3YrPct), FormatPct(r.AumChange5YrPct));
            tc.RowCountLabel.Text = $"{data.Count:N0} records";
        }
        else if (report.StartsWith("R9:"))
        {
            var data = await _svc.GetCompetitiveLandscapeAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Firm"), TC("CRD"), TC("State"), TC("Advisors", true), TC("AUM", true),
                TC("AUM/Adv", true), TC("Adv Share%", true), TC("AUM Share%", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.Name, r.CrdNumber, r.State, r.NumberOfAdvisors,
                    FormatAum(r.RegulatoryAum), FormatAum(r.AumPerAdvisor),
                    FormatPct(r.AdvisorMarketSharePct), FormatPct(r.AumMarketSharePct));
            tc.RowCountLabel.Text = $"{data.Count:N0} records";
        }
        else if (report.StartsWith("R10:"))
        {
            var data = await _svc.GetCredentialFrequencyAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Credential"), TC("Code"), TC("Advisors", true), TC("% of Market", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.Name, r.Code, r.AdvisorCount, FormatPct(r.PctOfActiveMarket));
            tc.RowCountLabel.Text = $"{data.Count:N0} records";
        }
        else if (report.StartsWith("R11:"))
        {
            var data = await _svc.GetGeographicDensityAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("State"), TC("Advisors", true), TC("Active", true),
                TC("Avg Exp(yrs)", true), TC("Disc Rate%", true), TC("Favorited", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.State, r.AdvisorCount, r.ActiveAdvisorCount,
                    r.AvgYearsExperience.HasValue ? $"{r.AvgYearsExperience:F1}" : "",
                    FormatPct(r.DisclosureRate), r.FavoritedCount);
            tc.RowCountLabel.Text = $"{data.Count:N0} states";
        }
        else if (report.StartsWith("R12:"))
        {
            var data = await _svc.GetAumConcentrationByGeoAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("State"), TC("Advisors", true), TC("Avg AUM/Adv", true), TC("Above $500M", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.State, r.AdvisorCount, FormatAum(r.AvgAumPerAdvisor), r.AdvisorsAbove500M);
            tc.RowCountLabel.Text = $"{data.Count:N0} states";
        }
        else if (report.StartsWith("R13:"))
        {
            var summaryTask = _svc.GetDisclosureProfileSummaryAsync(filter);
            var detailTask = _svc.GetDisclosureProfileDetailAsync(filter);
            await Task.WhenAll(summaryTask, detailTask);
            var summary = await summaryTask;
            var detail = await detailTask;

            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Name"), TC("CRD"), TC("State"), TC("Firm"), TC("#Disclosures", true),
                TC("Criminal"), TC("Regulatory"), TC("Civil"), TC("Complaints"),
                TC("Financial"), TC("Termination"), TC("Last Disclosure"), TC("AUM/Adv", true)
            });
            foreach (var r in detail)
                g.Rows.Add(r.FullName, r.CrdNumber, r.State, r.CurrentFirmName,
                    r.DisclosureCount,
                    r.HasCriminalDisclosure ? "✓" : "",
                    r.HasRegulatoryDisclosure ? "✓" : "",
                    r.HasCivilDisclosure ? "✓" : "",
                    r.HasCustomerComplaintDisclosure ? "✓" : "",
                    r.HasFinancialDisclosure ? "✓" : "",
                    r.HasTerminationDisclosure ? "✓" : "",
                    r.MostRecentDisclosureDate?.ToString("yyyy-MM-dd") ?? "",
                    FormatAum(r.AumPerAdvisor));
            tc.RowCountLabel.Text = $"{detail.Count:N0} records";
            if (summary != null)
            {
                tc.ErrorLabel.ForeColor = Color.Blue;
                tc.ErrorLabel.Text = $"Summary: {summary.AdvisorsWithDisclosures:N0}/{summary.TotalAdvisors:N0} with disclosures ({FormatPct(summary.DisclosureRate)})";
            }
        }
        else if (report.StartsWith("R14:"))
        {
            var data = await _svc.GetCleanRecordAdvisorsAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Name"), TC("CRD"), TC("State"), TC("Firm"), TC("Exp(yrs)", true),
                TC("Email"), TC("AUM/Adv", true), TC("BP Member"), TC("Tenure(yrs)", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.FullName, r.CrdNumber, r.State, r.CurrentFirmName,
                    r.YearsOfExperience, r.Email, FormatAum(r.AumPerAdvisor),
                    r.BrokerProtocolMember ? "✓" : "", r.TenureAtCurrentFirmYears);
            tc.RowCountLabel.Text = $"{data.Count:N0} records";
        }
        else if (report.StartsWith("R15:"))
        {
            var summaryTask = _svc.GetPipelineFunnelSummaryAsync(filter);
            var detailTask = _svc.GetPipelineFunnelDetailAsync(filter);
            await Task.WhenAll(summaryTask, detailTask);
            var summary = await summaryTask;
            var detail = await detailTask;

            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Name"), TC("CRD"), TC("State"), TC("Firm"), TC("Email"),
                TC("Exp(yrs)", true), TC("Disclosures"), TC("AUM/Adv", true)
            });
            foreach (var r in detail)
                g.Rows.Add(r.FullName, r.CrdNumber, r.State, r.CurrentFirmName,
                    r.Email, r.YearsOfExperience, r.HasDisclosures ? "✓" : "",
                    FormatAum(r.AumPerAdvisor));
            if (summary != null)
                tc.RowCountLabel.Text = $"Active: {summary.TotalActive:N0} | Favorited: {summary.Favorited:N0} | With Email: {summary.FavoritedWithEmail:N0} | In CRM: {summary.ImportedToCrm:N0}";
            else
                tc.RowCountLabel.Text = $"{detail.Count:N0} records";
        }
        else if (report.StartsWith("R16:"))
        {
            var data = await _svc.GetContactCoverageGapAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Name"), TC("CRD"), TC("State"), TC("Firm"), TC("Exp(yrs)", true),
                TC("Firm Phone"), TC("Website"), TC("AUM/Adv", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.FullName, r.CrdNumber, r.State, r.CurrentFirmName,
                    r.YearsOfExperience, r.FirmPhone, r.Website, FormatAum(r.AumPerAdvisor));
            tc.RowCountLabel.Text = $"{data.Count:N0} records";
        }
        else if (report.StartsWith("R17:"))
        {
            var data = await _svc.GetFirmStabilitySignalAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Firm"), TC("CRD"), TC("State"), TC("AUM", true), TC("Advisors", true),
                TC("Prior Count", true), TC("Last Filing"), TC("Days Since", true),
                TC("AUM∆1yr%", true), TC("Headcount∆", true), TC("Avg Disc", true),
                TC("Score", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.Name, r.CrdNumber, r.State, FormatAum(r.RegulatoryAum),
                    r.NumberOfAdvisors, r.PriorAdvisorCount, r.LatestFilingDate,
                    r.DaysSinceLastFiling, FormatPct(r.AumChange1YrPct),
                    r.HeadcountChange,
                    r.AvgAdvisorDisclosureCount.HasValue ? $"{r.AvgAdvisorDisclosureCount:F2}" : "",
                    r.InstabilityScore);
            tc.RowCountLabel.Text = $"{data.Count:N0} records";
        }
        else if (report.StartsWith("R18:"))
        {
            var data = await _svc.GetCompensationAnalysisAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Model"), TC("Firms", true), TC("Advisors", true),
                TC("Avg AUM", true), TC("Avg AUM/Adv", true), TC("Avg HNW%", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.CompensationModel, r.FirmCount, r.AdvisorCount,
                    FormatAum(r.AvgRegulatoryAum), FormatAum(r.AvgAumPerAdvisor),
                    FormatPct(r.AvgHnwClientPct));
            tc.RowCountLabel.Text = $"{data.Count:N0} models";
        }
        else if (report.StartsWith("R19:"))
        {
            var data = await _svc.GetHnwFocusFirmsAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Firm"), TC("CRD"), TC("State"), TC("Advisors", true), TC("AUM", true),
                TC("AUM/Adv", true), TC("HNW%", true), TC("Fee Only"), TC("BP"),
                TC("Priv Funds", true), TC("Score", true)
            });
            foreach (var r in data)
                g.Rows.Add(r.Name, r.CrdNumber, r.State, r.NumberOfAdvisors,
                    FormatAum(r.RegulatoryAum), FormatAum(r.AumPerAdvisor),
                    FormatPct(r.HnwClientPct),
                    r.CompensationFeeOnly == true ? "✓" : "",
                    r.BrokerProtocolMember == true ? "✓" : "",
                    r.PrivateFundCount, r.UpmarketScore);
            tc.RowCountLabel.Text = $"{data.Count:N0} records";
        }
        else if (report.StartsWith("R20:"))
        {
            var data = await _svc.GetMultiStateRegistrationMapAsync(filter);
            g.Columns.AddRange(new DataGridViewColumn[]
            {
                TC("Name"), TC("CRD"), TC("State"), TC("Firm"), TC("Exp(yrs)", true),
                TC("#States", true), TC("States"), TC("Tier")
            });
            foreach (var r in data)
                g.Rows.Add(r.FullName, r.CrdNumber, r.State, r.CurrentFirmName,
                    r.YearsOfExperience, r.StateRegistrationCount, r.RegisteredStates,
                    r.PortabilityTier);
            tc.RowCountLabel.Text = $"{data.Count:N0} records";
        }

        string reportId = report.Split(':')[0].Trim();
        ApplyColumnTooltips(reportId, g);
        ApplyColumnWidths(g);
        foreach (DataGridViewColumn col in g.Columns)
            col.SortMode = DataGridViewColumnSortMode.Automatic;
    }

    private static DataGridViewTextBoxColumn TC(string header, bool rightAlign = false)
    {
        var col = new DataGridViewTextBoxColumn { HeaderText = header, Name = header };
        if (rightAlign)
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        return col;
    }

    private static void UpdateDescriptionPanel(Panel descPanel, string reportItem)
    {
        var rtb = (RichTextBox)descPanel.Controls[0];
        rtb.Clear();
        if (string.IsNullOrEmpty(reportItem)) return;

        string id = reportItem.Split(':')[0].Trim();
        if (!_reportDescriptions.TryGetValue(id, out var desc)) return;

        RtbAppend(rtb, desc.Name + "\n", 10f, FontStyle.Bold);
        RtbAppend(rtb, desc.Description + "\n", 9f, FontStyle.Regular);
        RtbAppend(rtb, "Use case: " + desc.UseCase + "\n", 9f, FontStyle.Italic);
        RtbAppend(rtb, "Key metrics:\n", 9f, FontStyle.Regular);
        foreach (var m in desc.KeyMetrics)
            RtbAppend(rtb, $"  • {m}\n", 9f, FontStyle.Regular);

        rtb.SelectionStart = 0;
        rtb.ScrollToCaret();
    }

    private static void RtbAppend(RichTextBox rtb, string text, float size, FontStyle style)
    {
        rtb.SelectionStart = rtb.TextLength;
        rtb.SelectionLength = 0;
        using var f = new Font(rtb.Font.FontFamily, size, style);
        rtb.SelectionFont = f;
        rtb.AppendText(text);
    }

    private void ApplyColumnTooltips(string reportId, DataGridView g)
    {
        foreach (DataGridViewColumn col in g.Columns)
        {
            if (_columnTooltips.TryGetValue(col.Name, out var tip))
                col.ToolTipText = tip;
        }

        var scoreCol = g.Columns["Score"];
        if (scoreCol != null)
        {
            scoreCol.ToolTipText = reportId switch
            {
                "R1"  => "Composite flight risk score (0–100). Higher = more likely to move.",
                "R2"  => "Composite target value score (0–100). Higher = more valuable recruit.",
                "R17" => "Firm instability score (0–100). Higher = more unstable.",
                "R19" => "Upmarket focus score (0–100). Higher = more HNW-focused.",
                _     => "Composite score (0–100). Higher scores indicate stronger match for this report's criteria."
            };
        }
    }

    private static void ApplyColumnWidths(DataGridView g)
    {
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        g.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
        foreach (DataGridViewColumn col in g.Columns)
        {
            if (col.Width > 250)
                col.Width = 250;
        }
    }

    private static ReportFilter BuildFilter(TabPageControls tc)
    {
        var f = new ReportFilter
        {
            ActiveOnly = tc.ActiveOnlyCheck.Checked,
            NoDisclosuresOnly = tc.NoDisclosuresCheck.Checked,
            BrokerProtocolOnly = tc.BrokerProtocolCheck.Checked,
            FavoritedOnly = tc.FavoritedOnlyCheck.Checked,
        };
        if (!string.IsNullOrEmpty(tc.StateCombo.Text) && tc.FilterState.Visible)
            f.State = tc.StateCombo.Text;
        if (!string.IsNullOrEmpty(tc.RecordTypeCombo.Text) && tc.FilterRecordType.Visible)
            f.RecordType = tc.RecordTypeCombo.Text;
        if (tc.FilterMinAum.Visible && decimal.TryParse(tc.MinAumText.Text, out var aum))
            f.MinRegulatoryAum = aum * 1_000_000m;
        if (tc.FilterMinAdvisors.Visible && int.TryParse(tc.MinAdvisorsText.Text, out var minAdv))
            f.MinAdvisors = minAdv;
        if (tc.FilterMinYearsExp.Visible && int.TryParse(tc.MinYearsExpText.Text, out var minExp))
            f.MinYearsExperience = minExp;
        if (tc.FilterMinTotalFirmCount.Visible && int.TryParse(tc.MinTotalFirmCountText.Text, out var minFirms))
            f.MinTotalFirmCount = minFirms;
        if (tc.FilterMinHnwPct.Visible && int.TryParse(tc.MinHnwClientPctText.Text, out var minHnw))
            f.MinHnwClientPct = minHnw;
        if (tc.FilterMinStateRegCount.Visible && int.TryParse(tc.MinStateRegCountText.Text, out var minStates))
            f.MinStateRegistrationCount = minStates;
        if (!string.IsNullOrEmpty(tc.CompTypeCombo.Text) && tc.FilterCompType.Visible)
            f.CompensationType = tc.CompTypeCombo.Text;
        return f;
    }

    private static string FormatAum(decimal? v)
    {
        if (!v.HasValue || v == 0) return "";
        if (v >= 1_000_000_000m) return $"{v / 1_000_000_000m:F1}B";
        if (v >= 1_000_000m) return $"{v / 1_000_000m:F1}M";
        return $"{v / 1_000m:F1}k";
    }

    private static string FormatPct(decimal? v) => v.HasValue ? $"{v:F1}%" : "";

    private static string FormatRate(decimal? v) => v.HasValue ? $"{v:F2}" : "";

    public DataGridView GetGridForTab(int tabIndex) => _tabControls[tabIndex]!.Grid;

    public string GenerateCsvContent(int tabIndex)
    {
        var grid = _tabControls[tabIndex]!.Grid;
        var sb = new StringBuilder();
        var headers = grid.Columns.Cast<DataGridViewColumn>().Select(c => EscapeCsvField(c.HeaderText));
        sb.AppendLine(string.Join(",", headers));
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            var cells = row.Cells.Cast<DataGridViewCell>()
                .Select(c => EscapeCsvField(c.FormattedValue?.ToString() ?? ""));
            sb.AppendLine(string.Join(",", cells));
        }
        return sb.ToString();
    }

    private static string EscapeCsvField(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
