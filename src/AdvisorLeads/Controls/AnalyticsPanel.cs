using AdvisorLeads.Data;
using AdvisorLeads.Models;
using AdvisorLeads.Services;

namespace AdvisorLeads.Controls;

/// <summary>
/// Analytics &amp; Intelligence panel — four tabbed views: Geographic heat map,
/// Competitive Intelligence, Team Lifts, and Mobility scoring.
/// </summary>
public sealed class AnalyticsPanel : UserControl
{
    private readonly AdvisorRepository _repo;
    private readonly GeographicAggregationService _geoService;
    private readonly CompetitiveIntelligenceService _competitiveService;
    private readonly TeamLiftDetectionService _teamLiftService;
    private readonly MobilityScoreService _mobilityScoreService;

    // ── Geographic tab ──────────────────────────────────────────────────────
    private StateHeatMapPanel _heatMap   = null!;
    private DataGridView _geoGrid        = null!;
    private CheckBox _chkActiveOnly      = null!;

    // ── Competitive tab ─────────────────────────────────────────────────────
    private ComboBox _cmbCompState       = null!;
    private NumericUpDown _nudTopN       = null!;
    private DataGridView _compGrid       = null!;

    // ── Team Lifts tab ──────────────────────────────────────────────────────
    private NumericUpDown _nudLiftDays   = null!;
    private NumericUpDown _nudMinGroup   = null!;
    private ListView _liftListView       = null!;
    private DataGridView _liftDetailGrid = null!;

    // ── Mobility tab ────────────────────────────────────────────────────────
    private NumericUpDown _nudMinScore   = null!;
    private ComboBox _cmbMobState        = null!;
    private Button _btnRecalculate       = null!;
    private Label _lblMobStatus          = null!;
    private DataGridView _mobGrid        = null!;

    private static readonly string[] States =
    {
        "", "AL","AK","AZ","AR","CA","CO","CT","DC","DE","FL","GA",
        "HI","ID","IL","IN","IA","KS","KY","LA","ME","MD","MA","MI",
        "MN","MS","MO","MT","NE","NV","NH","NJ","NM","NY","NC","ND",
        "OH","OK","OR","PA","RI","SC","SD","TN","TX","UT","VT","VA",
        "WA","WV","WI","WY",
    };

    public AnalyticsPanel(
        AdvisorRepository repo,
        GeographicAggregationService geoService,
        CompetitiveIntelligenceService competitiveService,
        TeamLiftDetectionService teamLiftService,
        MobilityScoreService mobilityScoreService)
    {
        _repo               = repo;
        _geoService         = geoService;
        _competitiveService = competitiveService;
        _teamLiftService    = teamLiftService;
        _mobilityScoreService = mobilityScoreService;

        BuildUI();
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>Called by MainForm when the Analytics tab is first shown.</summary>
    public void LoadDefaultView() => LoadGeoData();

    // ── Build UI ────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        this.Dock = DockStyle.Fill;
        this.BackColor = Color.FromArgb(245, 247, 252);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildGeographicTab());
        tabs.TabPages.Add(BuildCompetitiveTab());
        tabs.TabPages.Add(BuildTeamLiftsTab());
        tabs.TabPages.Add(BuildMobilityTab());
        this.Controls.Add(tabs);
    }

    // ── Geographic tab ──────────────────────────────────────────────────────

    private TabPage BuildGeographicTab()
    {
        var page = new TabPage("Geographic") { Padding = new Padding(4) };

        var layout = new TableLayoutPanel
        {
            Dock       = DockStyle.Fill,
            ColumnCount = 1,
            RowCount   = 3,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Toolbar
        var toolbar = new FlowLayoutPanel
        {
            AutoSize  = true,
            Dock      = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            Padding   = new Padding(4),
        };
        var btnRefreshGeo = new Button { Text = "Refresh", AutoSize = true };
        btnRefreshGeo.Click += async (_, _) => await RefreshGeoAsync();
        _chkActiveOnly = new CheckBox { Text = "Active Only", Checked = true, AutoSize = true, Margin = new Padding(8, 4, 0, 0) };
        toolbar.Controls.AddRange(new Control[] { btnRefreshGeo, _chkActiveOnly });

        // Heat map panel (fixed size, centered in a scroll panel)
        _heatMap = new StateHeatMapPanel();
        var mapHost = new Panel
        {
            AutoScroll = true,
            BackColor  = Color.White,
            Height     = _heatMap.Height + 10,
        };
        mapHost.Controls.Add(_heatMap);
        _heatMap.Location = new Point(4, 4);

        // Geographic data grid
        _geoGrid = BuildReadOnlyGrid();
        _geoGrid.Dock = DockStyle.Fill;
        AddGeoGridColumns();

        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(mapHost, 0, 1);
        layout.Controls.Add(_geoGrid, 0, 2);
        page.Controls.Add(layout);
        return page;
    }

    private void AddGeoGridColumns()
    {
        _geoGrid.Columns.AddRange(
            Col("State",       "State",           60),
            Col("StateName",   "State Name",      130),
            Col("Advisors",    "Advisors",         80, right: true),
            Col("Active",      "Active",           70, right: true),
            Col("TotalAum",    "Total AUM",       110, right: true),
            Col("AvgAum",      "Avg AUM/Adv",     110, right: true),
            Col("DiscRate",    "Disc. Rate %",     90, right: true),
            Col("Favorited",   "Favorited",        70, right: true)
        );
    }

    private async Task RefreshGeoAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                var data = _geoService.GetStateAggregations(_chkActiveOnly.Checked);
                this.Invoke(() => PopulateGeoData(data));
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading geographic data:\n{ex.Message}",
                "Analytics", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void LoadGeoData()
    {
        try
        {
            var data = _geoService.GetStateAggregations(_chkActiveOnly.Checked);
            PopulateGeoData(data);
        }
        catch { /* non-critical — empty DB on first run */ }
    }

    private void PopulateGeoData(List<StateAggregation> data)
    {
        _heatMap.LoadData(data);

        _geoGrid.Rows.Clear();
        foreach (var s in data)
        {
            string totalAum = s.TotalAum.HasValue ? FormatHelpers.FormatAum(s.TotalAum.Value) : "—";
            string avgAum   = s.AvgAumPerAdvisor.HasValue ? FormatHelpers.FormatAum(s.AvgAumPerAdvisor.Value) : "—";
            string discRate = $"{s.DisclosureRate * 100:F1}%";
            _geoGrid.Rows.Add(s.StateCode, s.StateName, s.AdvisorCount, s.ActiveAdvisorCount,
                totalAum, avgAum, discRate, s.FavoritedCount);
        }
    }

    // ── Competitive Intelligence tab ────────────────────────────────────────

    private TabPage BuildCompetitiveTab()
    {
        var page = new TabPage("Competitive") { Padding = new Padding(4) };

        var layout = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 2,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Toolbar
        var toolbar = new FlowLayoutPanel
        {
            AutoSize      = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding       = new Padding(4),
        };

        toolbar.Controls.Add(new Label { Text = "State:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        _cmbCompState = new ComboBox { Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbCompState.Items.AddRange(States.Cast<object>().ToArray());
        _cmbCompState.SelectedIndex = 0;
        toolbar.Controls.Add(_cmbCompState);

        toolbar.Controls.Add(new Label { Text = "Top N:", AutoSize = true, Margin = new Padding(8, 6, 4, 0) });
        _nudTopN = new NumericUpDown { Minimum = 10, Maximum = 200, Value = 50, Width = 65, Margin = new Padding(0, 3, 0, 0) };
        toolbar.Controls.Add(_nudTopN);

        var btnRefreshComp = new Button { Text = "Refresh", AutoSize = true, Margin = new Padding(8, 3, 0, 0) };
        btnRefreshComp.Click += async (_, _) => await RefreshCompetitiveAsync();
        toolbar.Controls.Add(btnRefreshComp);

        // Grid
        _compGrid = BuildReadOnlyGrid();
        _compGrid.Dock = DockStyle.Fill;
        _compGrid.CellFormatting += OnCompGridFormatting;
        AddCompGridColumns();

        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(_compGrid, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private void AddCompGridColumns()
    {
        _compGrid.Columns.AddRange(
            Col("colFirmName",   "Firm Name",           200),
            Col("colState",      "State",                50),
            Col("colCurrent",    "Advisors",             80,  right: true),
            Col("colChange1yr",  "1yr Change",           75,  right: true),
            Col("colAum",        "Current AUM",         110,  right: true),
            Col("colAumChg",     "AUM Chg 1yr",         110,  right: true),
            Col("colAumPct",     "AUM Chg %",            80,  right: true),
            Col("colGrowth",     "Trend",                90)
        );
    }

    private async Task RefreshCompetitiveAsync()
    {
        try
        {
            var state = _cmbCompState.SelectedItem?.ToString() ?? string.Empty;
            var topN  = (int)_nudTopN.Value;

            await Task.Run(() =>
            {
                var data = _competitiveService.GetFirmGrowthShrinkData(
                    string.IsNullOrEmpty(state) ? null : state, topN);
                this.Invoke(() => PopulateCompetitiveData(data));
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading competitive data:\n{ex.Message}",
                "Analytics", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void PopulateCompetitiveData(List<FirmGrowthSnapshot> data)
    {
        _compGrid.Rows.Clear();
        foreach (var f in data)
        {
            string aum    = f.CurrentAum.HasValue    ? FormatHelpers.FormatAum(f.CurrentAum.Value)    : "—";
            string aumChg = f.AumChange1Yr.HasValue  ? FormatHelpers.FormatAum(Math.Abs(f.AumChange1Yr.Value)) : "—";
            if (f.AumChange1Yr.HasValue && f.AumChange1Yr < 0) aumChg = $"-{aumChg}";
            string aumPct = f.AumChangePct1Yr.HasValue ? $"{f.AumChangePct1Yr.Value:F1}%" : "—";

            string trend;
            Color trendColor;
            if (f.IsGrowing)        { trend = "▲ Growing";  trendColor = Color.DarkGreen; }
            else if (f.IsShrinking) { trend = "▼ Shrinking"; trendColor = Color.DarkRed; }
            else                    { trend = "— Stable";    trendColor = Color.Gray; }

            int rowIdx = _compGrid.Rows.Add(
                f.FirmName, f.State ?? "",
                f.CurrentAdvisorCount?.ToString() ?? "—",
                f.AdvisorCountChange.HasValue ? f.AdvisorCountChange.Value.ToString("+#;-#;0") : "—",
                aum, aumChg, aumPct, trend);

            // Store trend colour on the row so CellFormatting can apply it quickly.
            _compGrid.Rows[rowIdx].Tag = trendColor;
        }
    }

    private void OnCompGridFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _compGrid.Rows.Count) return;
        if (e.CellStyle is null) return;
        var row = _compGrid.Rows[e.RowIndex];
        var colName = _compGrid.Columns[e.ColumnIndex].Name;

        if (colName == "colChange1yr")
        {
            var val = row.Cells["colChange1yr"].Value?.ToString() ?? "";
            if (val.StartsWith("+") || (int.TryParse(val, out var n) && n > 0))
                e.CellStyle.ForeColor = Color.DarkGreen;
            else if (val.StartsWith("-"))
                e.CellStyle.ForeColor = Color.DarkRed;
        }
        else if (colName is "colAumChg" or "colAumPct")
        {
            var val = row.Cells[colName].Value?.ToString() ?? "";
            if (val.StartsWith("-"))
                e.CellStyle.ForeColor = Color.DarkRed;
            else if (!val.StartsWith("—") && val != "0%")
                e.CellStyle.ForeColor = Color.DarkGreen;
        }
        else if (colName == "colGrowth")
        {
            if (row.Tag is Color trendColor)
                e.CellStyle.ForeColor = trendColor;
        }
    }

    // ── Team Lifts tab ──────────────────────────────────────────────────────

    private TabPage BuildTeamLiftsTab()
    {
        var page = new TabPage("Team Lifts") { Padding = new Padding(4) };

        var outer = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 2,
        };
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Toolbar
        var toolbar = new FlowLayoutPanel
        {
            AutoSize      = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding       = new Padding(4),
        };
        toolbar.Controls.Add(new Label { Text = "Window (days):", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        _nudLiftDays = new NumericUpDown { Minimum = 30, Maximum = 365, Value = 90, Width = 65, Margin = new Padding(0, 3, 0, 0) };
        toolbar.Controls.Add(_nudLiftDays);

        toolbar.Controls.Add(new Label { Text = "Min Group:", AutoSize = true, Margin = new Padding(8, 6, 4, 0) });
        _nudMinGroup = new NumericUpDown { Minimum = 2, Maximum = 10, Value = 2, Width = 55, Margin = new Padding(0, 3, 0, 0) };
        toolbar.Controls.Add(_nudMinGroup);

        var btnRefreshLifts = new Button { Text = "Refresh", AutoSize = true, Margin = new Padding(8, 3, 0, 0) };
        btnRefreshLifts.Click += async (_, _) => await RefreshTeamLiftsAsync();
        toolbar.Controls.Add(btnRefreshLifts);

        // SplitContainer: top = lift events, bottom = member detail
        var split = new SplitContainer
        {
            Dock        = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
        };

        _liftListView = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = true,
            MultiSelect   = false,
        };
        _liftListView.Columns.Add("From Firm",     200);
        _liftListView.Columns.Add("# Members",      70);
        _liftListView.Columns.Add("Window",         130);
        _liftListView.Columns.Add("Member Names",   400);
        _liftListView.SelectedIndexChanged += OnLiftSelected;
        split.Panel1.Controls.Add(_liftListView);

        _liftDetailGrid = BuildReadOnlyGrid();
        _liftDetailGrid.Dock = DockStyle.Fill;
        _liftDetailGrid.Columns.AddRange(
            Col("lColName",     "Advisor Name",    160),
            Col("lColCrd",      "CRD",              80),
            Col("lColFrom",     "Prior Firm",      190),
            Col("lColTo",       "New Firm",        190),
            Col("lColDate",     "Detected",        110)
        );
        split.Panel2.Controls.Add(_liftDetailGrid);

        outer.Controls.Add(toolbar, 0, 0);
        outer.Controls.Add(split, 0, 1);
        outer.SetRow(split, 1);

        // Set an initial SplitterDistance once the container is sized.
        split.SizeChanged += (_, _) =>
        {
            if (split.Height > split.Panel1MinSize + split.Panel2MinSize + split.SplitterWidth)
                split.SplitterDistance = split.Height / 2;
        };

        page.Controls.Add(outer);
        return page;
    }

    private List<TeamLiftEvent> _liftEvents = new();

    private async Task RefreshTeamLiftsAsync()
    {
        try
        {
            var days  = (int)_nudLiftDays.Value;
            var group = (int)_nudMinGroup.Value;

            await Task.Run(() =>
            {
                var data = _teamLiftService.DetectRecentLifts(days, group);
                this.Invoke(() => PopulateTeamLifts(data));
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error detecting team lifts:\n{ex.Message}",
                "Analytics", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void PopulateTeamLifts(List<TeamLiftEvent> events)
    {
        _liftEvents = events;
        _liftListView.Items.Clear();
        _liftDetailGrid.Rows.Clear();

        foreach (var ev in events)
        {
            string window = $"{ev.EarliestDeparture:MMM d} – {ev.LatestDeparture:MMM d, yyyy}";
            string names  = string.Join(", ",
                ev.Members.Select(m => m.AdvisorName ?? m.AdvisorCrd).Take(6));
            if (ev.Members.Count > 6) names += "…";

            var item = new ListViewItem(ev.FromFirmName);
            item.SubItems.Add(ev.Members.Count.ToString());
            item.SubItems.Add(window);
            item.SubItems.Add(names);
            _liftListView.Items.Add(item);
        }
    }

    private void OnLiftSelected(object? sender, EventArgs e)
    {
        _liftDetailGrid.Rows.Clear();
        if (_liftListView.SelectedIndices.Count == 0) return;
        int idx = _liftListView.SelectedIndices[0];
        if (idx < 0 || idx >= _liftEvents.Count) return;

        foreach (var m in _liftEvents[idx].Members)
        {
            _liftDetailGrid.Rows.Add(
                m.AdvisorName ?? "—",
                m.AdvisorCrd,
                m.FromFirmName ?? "—",
                m.ToFirmName   ?? "—",
                m.DetectedAt.ToLocalTime().ToString("yyyy-MM-dd"));
        }
    }

    // ── Mobility tab ────────────────────────────────────────────────────────

    private TabPage BuildMobilityTab()
    {
        var page = new TabPage("Mobility") { Padding = new Padding(4) };

        var layout = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 2,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Toolbar
        var toolbar = new FlowLayoutPanel
        {
            AutoSize      = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding       = new Padding(4),
        };

        toolbar.Controls.Add(new Label { Text = "Min Score:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        _nudMinScore = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 50, Width = 60, Margin = new Padding(0, 3, 0, 0) };
        toolbar.Controls.Add(_nudMinScore);

        toolbar.Controls.Add(new Label { Text = "State:", AutoSize = true, Margin = new Padding(8, 6, 4, 0) });
        _cmbMobState = new ComboBox { Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbMobState.Items.AddRange(States.Cast<object>().ToArray());
        _cmbMobState.SelectedIndex = 0;
        toolbar.Controls.Add(_cmbMobState);

        var btnRefreshMob = new Button { Text = "Refresh", AutoSize = true, Margin = new Padding(8, 3, 0, 0) };
        btnRefreshMob.Click += (_, _) => LoadMobilityData();
        toolbar.Controls.Add(btnRefreshMob);

        _btnRecalculate = new Button { Text = "Recalculate All Scores", AutoSize = true, Margin = new Padding(8, 3, 0, 0) };
        _btnRecalculate.Click += OnRecalculateScores;
        toolbar.Controls.Add(_btnRecalculate);

        _lblMobStatus = new Label { AutoSize = true, Margin = new Padding(8, 6, 0, 0), ForeColor = Color.DimGray };
        toolbar.Controls.Add(_lblMobStatus);

        // Grid
        _mobGrid = BuildReadOnlyGrid();
        _mobGrid.Dock = DockStyle.Fill;
        _mobGrid.CellFormatting += OnMobGridFormatting;
        _mobGrid.Columns.AddRange(
            Col("mColName",    "Name",              150),
            Col("mColFirm",    "Current Firm",      180),
            Col("mColState",   "State",              50),
            Col("mColScore",   "Mobility Score",     90, right: true),
            Col("mColYrs",     "Yrs at Firm",        75, right: true),
            Col("mColFirms",   "Total Firms",        75, right: true),
            Col("mColDisc",    "Disc. Score",        85, right: true)
        );

        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(_mobGrid, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private void LoadMobilityData()
    {
        try
        {
            int minScore = (int)_nudMinScore.Value;
            string state = _cmbMobState.SelectedItem?.ToString() ?? string.Empty;

            var advisors = _repo.GetActiveAdvisors();
            var filtered = advisors
                .Where(a => (a.MobilityScore ?? 0) >= minScore)
                .Where(a => string.IsNullOrEmpty(state) || a.State == state)
                .OrderByDescending(a => a.MobilityScore ?? 0)
                .ToList();

            _mobGrid.Rows.Clear();
            foreach (var a in filtered)
            {
                double yrsAtFirm = a.CurrentFirmStartDate.HasValue
                    ? (DateTime.UtcNow - a.CurrentFirmStartDate.Value).TotalDays / 365.25
                    : double.NaN;
                string yrsText = double.IsNaN(yrsAtFirm) ? "—" : $"{yrsAtFirm:F1}";

                _mobGrid.Rows.Add(
                    a.FullName,
                    a.CurrentFirmName ?? "—",
                    a.State ?? "—",
                    a.MobilityScore ?? 0,
                    yrsText,
                    a.TotalFirmCount?.ToString() ?? "—",
                    a.DisclosureSeverityScore?.ToString() ?? "—");
            }
        }
        catch { /* non-critical on empty DB */ }
    }

    private async void OnRecalculateScores(object? sender, EventArgs e)
    {
        try
        {
            _btnRecalculate.Enabled = false;
            _lblMobStatus.Text      = "Recalculating…";

            var progress = new Progress<string>(msg =>
            {
                if (this.IsHandleCreated)
                    this.Invoke(() => _lblMobStatus.Text = msg);
            });

            await Task.Run(() => _mobilityScoreService.RefreshAllScoresAsync(progress));
            LoadMobilityData();
            _lblMobStatus.Text = "Done.";
        }
        catch (Exception ex)
        {
            _lblMobStatus.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _btnRecalculate.Enabled = true;
        }
    }

    private void OnMobGridFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_mobGrid.Columns[e.ColumnIndex].Name != "mColScore") return;
        if (e.CellStyle is null) return;

        if (e.Value is int score)
        {
            e.CellStyle.BackColor = score >= 67
                ? Color.FromArgb(255, 200, 200)
                : score >= 34
                    ? Color.FromArgb(255, 235, 175)
                    : Color.FromArgb(200, 240, 200);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static DataGridView BuildReadOnlyGrid()
    {
        return new DataGridView
        {
            ReadOnly          = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            SelectionMode     = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            BorderStyle       = BorderStyle.None,
            BackgroundColor   = Color.White,
        };
    }

    private static DataGridViewTextBoxColumn Col(
        string name, string header, int width, bool right = false)
    {
        return new DataGridViewTextBoxColumn
        {
            Name              = name,
            HeaderText        = header,
            Width             = width,
            DefaultCellStyle  = right
                ? new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
                : new DataGridViewCellStyle(),
        };
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
