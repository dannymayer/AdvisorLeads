using AdvisorLeads.Services;

namespace AdvisorLeads.Controls;

/// <summary>
/// GDI+-painted US state tile map that colors states by advisor count (heat map).
/// Uses a fixed rectangular grid — no WebView2 or external dependencies required.
/// </summary>
public sealed class StateHeatMapPanel : Panel
{
    // Standard US tile-map grid positions (col, row) — 12 cols × 7 rows, 0-indexed.
    private static readonly Dictionary<string, (int Col, int Row)> StateGrid =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["ME"] = (11, 0), ["VT"] = (9,  0),
        ["WA"] = (0,  1), ["ID"] = (1,  1), ["MT"] = (2,  1), ["ND"] = (3,  1),
        ["MN"] = (4,  1), ["WI"] = (5,  1), ["MI"] = (6,  1), ["NH"] = (10, 1),
        ["OR"] = (0,  2), ["NV"] = (1,  2), ["WY"] = (2,  2), ["SD"] = (3,  2),
        ["IA"] = (4,  2), ["IL"] = (5,  2), ["IN"] = (6,  2), ["OH"] = (7,  2),
        ["PA"] = (8,  2), ["NY"] = (9,  2), ["MA"] = (10, 2), ["RI"] = (11, 2),
        ["CA"] = (0,  3), ["UT"] = (1,  3), ["CO"] = (2,  3), ["NE"] = (3,  3),
        ["MO"] = (4,  3), ["KY"] = (5,  3), ["WV"] = (6,  3), ["VA"] = (7,  3),
        ["MD"] = (8,  3), ["NJ"] = (9,  3), ["CT"] = (10, 3),
        ["AZ"] = (1,  4), ["NM"] = (2,  4), ["KS"] = (3,  4), ["AR"] = (4,  4),
        ["TN"] = (5,  4), ["NC"] = (6,  4), ["SC"] = (7,  4), ["DE"] = (8,  4),
        ["DC"] = (9,  4),
        ["OK"] = (3,  5), ["LA"] = (4,  5), ["MS"] = (5,  5), ["AL"] = (6,  5),
        ["GA"] = (7,  5),
        ["AK"] = (0,  6), ["HI"] = (1,  6), ["TX"] = (3,  6), ["FL"] = (8,  6),
    };

    private const int CellW    = 48;
    private const int CellH    = 32;
    private const int CellGap  = 2;
    private const int StepX    = CellW + CellGap;
    private const int StepY    = CellH + CellGap;
    private const int MapCols  = 12;
    private const int MapRows  = 7;
    private const int LegendH  = 42;

    // White → light blue → medium blue → dark blue → very dark blue
    private static readonly Color[] HeatColors =
    {
        Color.FromArgb(240, 248, 255),
        Color.FromArgb(179, 215, 255),
        Color.FromArgb(99,  162, 235),
        Color.FromArgb(36,  107, 210),
        Color.FromArgb(10,   56, 140),
    };

    private List<StateAggregation> _data = new();
    private readonly Dictionary<string, StateAggregation> _byState =
        new(StringComparer.OrdinalIgnoreCase);
    private int _maxCount = 1;

    private readonly ToolTip _toolTip = new();
    private string _lastHit = string.Empty;

    public StateHeatMapPanel()
    {
        this.DoubleBuffered = true;
        this.BackColor      = Color.White;
        this.Size           = new Size(MapCols * StepX + CellGap, MapRows * StepY + CellGap + LegendH);
        this.MouseMove     += OnMouseMoveMap;
    }

    /// <summary>Replaces the displayed data and repaints the map.</summary>
    public void LoadData(List<StateAggregation> data)
    {
        _data = data ?? new List<StateAggregation>();
        _byState.Clear();
        foreach (var s in _data)
            _byState[s.StateCode] = s;
        _maxCount = _data.Count > 0 ? Math.Max(1, _data.Max(s => s.AdvisorCount)) : 1;
        this.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;

        using var borderPen  = new Pen(Color.FromArgb(150, 150, 150), 1f);
        using var stateFont  = new Font("Segoe UI", 6.5f, FontStyle.Bold);

        foreach (var kvp in StateGrid)
        {
            var (col, row) = kvp.Value;
            var rect = CellRect(col, row);

            var fill = HeatColorFor(kvp.Key);
            using var fillBrush = new SolidBrush(fill);
            g.FillRectangle(fillBrush, rect);
            g.DrawRectangle(borderPen, rect);

            var textColor = fill.GetBrightness() < 0.45f ? Color.White : Color.FromArgb(30, 30, 30);
            using var textBrush = new SolidBrush(textColor);
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString(kvp.Key, stateFont, textBrush, RectangleF.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom), sf);
        }

        DrawLegend(g);
    }

    private void DrawLegend(Graphics g)
    {
        int legendY = MapRows * StepY + CellGap + 8;
        int x       = CellGap;

        using var labelFont  = new Font("Segoe UI", 7f);
        using var labelBrush = new SolidBrush(Color.FromArgb(60, 60, 60));
        using var borderPen  = new Pen(Color.Gray, 1f);

        g.DrawString("Advisors:", labelFont, labelBrush, x, legendY);
        x += 56;

        int swW = 38;
        int swH = 12;
        string[] labels = { "Low", "", "", "", "High" };
        for (int i = 0; i < HeatColors.Length; i++)
        {
            var sr = new Rectangle(x + i * (swW + 2), legendY, swW, swH);
            using var sb = new SolidBrush(HeatColors[i]);
            g.FillRectangle(sb, sr);
            g.DrawRectangle(borderPen, sr);
            if (labels[i].Length > 0)
                g.DrawString(labels[i], labelFont, labelBrush, sr.X, legendY + swH + 2);
        }
    }

    private Color HeatColorFor(string code)
    {
        if (!_byState.TryGetValue(code, out var agg) || agg.AdvisorCount == 0)
            return HeatColors[0];
        double ratio = (double)agg.AdvisorCount / _maxCount;
        int idx = Math.Clamp((int)(ratio * (HeatColors.Length - 1)), 0, HeatColors.Length - 1);
        return HeatColors[idx];
    }

    private static Rectangle CellRect(int col, int row) =>
        new(col * StepX + CellGap, row * StepY + CellGap, CellW, CellH);

    private void OnMouseMoveMap(object? sender, MouseEventArgs e)
    {
        var hit = HitTest(e.X, e.Y);
        var key = hit ?? string.Empty;
        if (key == _lastHit) return;
        _lastHit = key;

        if (hit != null && _byState.TryGetValue(hit, out var agg))
        {
            string aumText = FormatAum(agg.TotalAum);
            _toolTip.SetToolTip(this,
                $"{agg.StateCode} — {agg.StateName}\n" +
                $"Advisors: {agg.AdvisorCount:N0}\n" +
                $"Active: {agg.ActiveAdvisorCount:N0}\n" +
                $"Total AUM: {aumText}\n" +
                $"Favorited: {agg.FavoritedCount}");
        }
        else
        {
            _toolTip.SetToolTip(this, string.Empty);
        }
    }

    private string? HitTest(int x, int y)
    {
        foreach (var kvp in StateGrid)
        {
            var (col, row) = kvp.Value;
            if (CellRect(col, row).Contains(x, y))
                return kvp.Key;
        }
        return null;
    }

    private static string FormatAum(decimal? aum)
    {
        if (!aum.HasValue || aum.Value == 0) return "—";
        if (aum.Value >= 1_000_000_000_000m) return $"${aum.Value / 1_000_000_000_000m:F1}T";
        if (aum.Value >= 1_000_000_000m)     return $"${aum.Value / 1_000_000_000m:F1}B";
        if (aum.Value >= 1_000_000m)         return $"${aum.Value / 1_000_000m:F1}M";
        return $"${aum.Value:N0}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _toolTip.Dispose();
        base.Dispose(disposing);
    }
}
