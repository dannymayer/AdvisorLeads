using ClosedXML.Excel;
using AdvisorLeads.Models;
using AdvisorLeads.Services;

namespace AdvisorLeads.Tests.Services;

public class ExportServiceTests
{
    private static Advisor MakeAdvisor(string first = "Jane", string last = "Doe",
        bool hasDisclosures = false, bool isFavorited = false, bool isExcluded = false) =>
        new()
        {
            FirstName = first,
            LastName = last,
            CrdNumber = "12345",
            CurrentFirmName = "Acme, Inc.",
            State = "CA",
            City = "Los Angeles",
            HasDisclosures = hasDisclosures,
            DisclosureCount = hasDisclosures ? 2 : 0,
            IsFavorited = isFavorited,
            IsExcluded = isExcluded,
            UpdatedAt = new DateTime(2024, 1, 15),
            CreatedAt = new DateTime(2023, 6, 1)
        };

    private static Firm MakeFirm(string name = "Test Firm LLC") =>
        new()
        {
            CrdNumber = "999",
            Name = name,
            State = "NY",
            City = "New York",
            Source = "SEC",
            UpdatedAt = new DateTime(2024, 2, 1),
            CreatedAt = new DateTime(2023, 1, 1)
        };

    [Fact]
    public void ExportService_ExportToCsv_CreatesFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.csv");
        try
        {
            var cols = AdvisorExportColumns.GetPreset("Default");
            ExportService.ExportToCsv(new[] { MakeAdvisor() }, cols, path);
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ExportService_ExportToCsv_WritesHeaderRow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.csv");
        try
        {
            var cols = AdvisorExportColumns.GetPreset("Default");
            ExportService.ExportToCsv(new[] { MakeAdvisor() }, cols, path);
            var lines = File.ReadAllLines(path);
            Assert.True(lines.Length >= 2, "Expected header + at least 1 data row");
            Assert.Contains("Full Name", lines[0]);
            Assert.Contains("Current Firm", lines[0]);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ExportService_ExportToCsv_EscapesCommasInValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.csv");
        try
        {
            var cols = new List<ExportColumnDefinition<Advisor>>
            {
                new("FullName", "Full Name", a => a.FullName),
                new("CurrentFirmName", "Current Firm", a => a.CurrentFirmName)
            };
            var advisor = MakeAdvisor();
            // Firm name with a comma should be quoted in CSV output
            ExportService.ExportToCsv(new[] { advisor }, cols, path);
            var content = File.ReadAllText(path);
            // "Acme, Inc." contains a comma and should be quoted
            Assert.Contains("\"Acme, Inc.\"", content);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ExportService_ExportToExcel_CreatesValidXlsx()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xlsx");
        try
        {
            var cols = AdvisorExportColumns.GetPreset("Default");
            ExportService.ExportToExcel(new[] { MakeAdvisor() }, cols, path, sheetName: "Test");
            Assert.True(File.Exists(path));

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("Test");
            Assert.NotNull(ws);
            // Header row
            Assert.Equal("Full Name", ws.Cell(1, 1).GetString());
            // Data row
            Assert.Equal("Jane Doe", ws.Cell(2, 1).GetString());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void AdvisorExportColumns_DefaultPreset_HasExpectedColumns()
    {
        var preset = AdvisorExportColumns.GetPreset("Default");
        var keys = preset.Select(c => c.Key).ToList();
        Assert.Contains("FullName", keys);
        Assert.Contains("CurrentFirmName", keys);
        Assert.Contains("State", keys);
        Assert.Contains("HasDisclosures", keys);
        Assert.Contains("UpdatedAt", keys);
        // Ensure it's not the full set
        Assert.True(preset.Count < AdvisorExportColumns.All.Count);
    }

    [Fact]
    public void FirmExportColumns_DefaultPreset_HasExpectedColumns()
    {
        var preset = FirmExportColumns.GetPreset("Default");
        var keys = preset.Select(c => c.Key).ToList();
        Assert.Contains("Name", keys);
        Assert.Contains("CrdNumber", keys);
        Assert.Contains("State", keys);
        Assert.Contains("RegistrationStatus", keys);
        // Ensure it's not the full set
        Assert.True(preset.Count < FirmExportColumns.All.Count);
    }

    [Fact]
    public void ExportService_GetAdvisorRowStyle_ReturnsCorrectStyle()
    {
        Assert.Equal(ExcelRowStyle.Excluded, ExportService.GetAdvisorRowStyle(MakeAdvisor(isExcluded: true)));
        Assert.Equal(ExcelRowStyle.Disclosure, ExportService.GetAdvisorRowStyle(MakeAdvisor(hasDisclosures: true)));
        Assert.Equal(ExcelRowStyle.Favorited, ExportService.GetAdvisorRowStyle(MakeAdvisor(isFavorited: true)));
        Assert.Equal(ExcelRowStyle.Normal, ExportService.GetAdvisorRowStyle(MakeAdvisor()));
    }
}
