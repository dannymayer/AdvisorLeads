using AdvisorLeads.Controls;
using AdvisorLeads.Services;

namespace AdvisorLeads.Tests.Controls;

public class ReportsPanelTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ReportingService _svc;

    public ReportsPanelTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"rp_test_{Guid.NewGuid():N}.db");
        _svc = new ReportingService(_dbPath);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public void ReportsPanel_Constructor_DoesNotThrow()
    {
        var panel = new ReportsPanel(_svc);
        Assert.NotNull(panel);
        panel.Dispose();
    }

    [Fact]
    public void ReportsPanel_TabCount_IsSix()
    {
        var panel = new ReportsPanel(_svc);
        var tabs = panel.Controls.OfType<TabControl>().First();
        Assert.Equal(6, tabs.TabCount);
        panel.Dispose();
    }

    [Fact]
    public void ReportsPanel_AllTabsHaveReportList()
    {
        var panel = new ReportsPanel(_svc);
        var tabs = panel.Controls.OfType<TabControl>().First();
        foreach (TabPage page in tabs.TabPages)
        {
            var split = page.Controls.OfType<SplitContainer>().First();
            var listBox = split.Panel1.Controls.OfType<ListBox>().FirstOrDefault()
                ?? split.Panel1.Controls.OfType<Panel>()
                    .SelectMany(p => p.Controls.OfType<ListBox>()).FirstOrDefault();
            Assert.NotNull(listBox);
            Assert.True(listBox.Items.Count > 0, $"Tab '{page.Text}' ListBox should have report items");
        }
        panel.Dispose();
    }

    [Fact]
    public void ReportsPanel_ExportCsv_WritesHeaderRow()
    {
        var panel = new ReportsPanel(_svc);
        var grid = panel.GetGridForTab(0);
        grid.Columns.Clear();
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Name" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Score", HeaderText = "Score" });
        grid.Rows.Add("John Doe", "85");

        var csv = panel.GenerateCsvContent(0);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.True(lines.Length >= 2, "CSV should have header + at least one data row");
        Assert.Contains("Name", lines[0]);
        Assert.Contains("Score", lines[0]);
        panel.Dispose();
    }
}
