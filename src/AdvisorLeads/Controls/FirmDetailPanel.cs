using AdvisorLeads.Models;

namespace AdvisorLeads.Controls;

public class FirmDetailPanel : UserControl
{
    private Firm? _firm;

    private Label _lblName = null!;
    private Label _lblCrd = null!;
    private Label _lblSourceBadge = null!;
    private Label _lblTypeBadge = null!;
    private Label _lblStatusBadge = null!;
    private Label _lblBpBadge = null!;
    private TableLayoutPanel _infoGrid = null!;

    public FirmDetailPanel()
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
            Location = new Point(0, 0),
            ForeColor = Color.FromArgb(30, 30, 80)
        };
        header.Controls.Add(_lblName);

        _lblCrd = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9),
            AutoSize = true,
            Location = new Point(2, 34),
            ForeColor = Color.Gray
        };
        header.Controls.Add(_lblCrd);

        _lblSourceBadge = MakeBadge("SEC", Color.FromArgb(0, 120, 215));
        _lblSourceBadge.Location = new Point(2, 56);
        header.Controls.Add(_lblSourceBadge);

        _lblTypeBadge = MakeBadge("Investment Advisor", Color.FromArgb(0, 150, 100));
        _lblTypeBadge.Location = new Point(50, 56);
        header.Controls.Add(_lblTypeBadge);

        _lblStatusBadge = MakeBadge("", Color.Gray);
        _lblStatusBadge.Location = new Point(200, 56);
        _lblStatusBadge.Visible = false;
        header.Controls.Add(_lblStatusBadge);

        _lblBpBadge = MakeBadge("Broker Protocol ✓", Color.FromArgb(0, 100, 170));
        _lblBpBadge.Location = new Point(350, 56);
        _lblBpBadge.Visible = false;
        header.Controls.Add(_lblBpBadge);

        mainLayout.Controls.Add(header, 0, 0);

        // ── Info grid ──
        _infoGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            AutoScroll = true,
            Padding = new Padding(0, 8, 0, 0)
        };
        _infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        _infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        _infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        mainLayout.Controls.Add(_infoGrid, 0, 1);

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
            _lblStatusBadge.Location = new Point(4 + _lblTypeBadge.Right, 56);
        }
        else
        {
            _lblStatusBadge.Visible = false;
        }

        if (firm.BrokerProtocolMember)
        {
            _lblBpBadge.Location = new Point((_lblStatusBadge.Visible ? _lblStatusBadge.Right : _lblTypeBadge.Right) + 4, 56);
            _lblBpBadge.Visible = true;
        }
        else
        {
            _lblBpBadge.Visible = false;
        }

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
        AddRow("Source:", firm.Source ?? "", "Record Type:", firm.RecordType ?? "");

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
