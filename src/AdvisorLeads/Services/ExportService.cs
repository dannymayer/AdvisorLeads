using ClosedXML.Excel;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

public enum ExcelRowStyle { Normal, Disclosure, Excluded, Favorited }

public static class ExportService
{
    // ── CSV ───────────────────────────────────────────────────────────────

    public static void ExportToCsv<T>(
        IEnumerable<T> records,
        IEnumerable<ExportColumnDefinition<T>> columns,
        string filePath)
    {
        var cols = columns.ToList();
        // UTF-8 BOM so Excel auto-detects encoding
        using var sw = new StreamWriter(filePath, false, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        sw.WriteLine(string.Join(",", cols.Select(c => CsvEscape(c.Header))));
        foreach (var record in records)
            sw.WriteLine(string.Join(",", cols.Select(c => CsvEscape(c.Selector(record)?.ToString()))));
    }

    // ── Excel ─────────────────────────────────────────────────────────────

    public static void ExportToExcel<T>(
        IEnumerable<T> records,
        IEnumerable<ExportColumnDefinition<T>> columns,
        string filePath,
        string sheetName = "Export",
        bool applyConditionalFormatting = false,
        Func<T, ExcelRowStyle>? rowStyleSelector = null)
    {
        var cols = columns.ToList();
        var data = records.ToList();

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(sheetName);

        // Header row: bold, blue background, white text
        for (int c = 0; c < cols.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = cols[c].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(68, 114, 196);
            cell.Style.Font.FontColor = XLColor.White;
        }

        // Data rows
        for (int r = 0; r < data.Count; r++)
        {
            var record = data[r];
            var rowStyle = rowStyleSelector?.Invoke(record) ?? ExcelRowStyle.Normal;

            for (int c = 0; c < cols.Count; c++)
            {
                var cell = ws.Cell(r + 2, c + 1);
                var value = cols[c].Selector(record);

                switch (value)
                {
                    case DateTime dt:
                        cell.Value = dt;
                        break;
                    case decimal dec:
                        cell.Value = dec;
                        break;
                    case int i:
                        cell.Value = i;
                        break;
                    case long l:
                        cell.Value = l;
                        break;
                    case bool b:
                        cell.Value = b ? "Yes" : "No";
                        break;
                    default:
                        cell.Value = value?.ToString() ?? string.Empty;
                        break;
                }

                if (applyConditionalFormatting && rowStyle != ExcelRowStyle.Normal)
                {
                    cell.Style.Fill.BackgroundColor = rowStyle switch
                    {
                        ExcelRowStyle.Disclosure => XLColor.FromArgb(255, 235, 235),
                        ExcelRowStyle.Excluded   => XLColor.FromArgb(245, 245, 245),
                        ExcelRowStyle.Favorited  => XLColor.FromArgb(255, 252, 220),
                        _                        => XLColor.NoColor
                    };
                }
            }
        }

        if (cols.Count > 0)
        {
            ws.RangeUsed()?.SetAutoFilter();
            ws.SheetView.FreezeRows(1);
            // Auto-fit column widths; ClosedXML caps at 60 chars
            ws.ColumnsUsed().AdjustToContents(1, 60);
        }

        wb.SaveAs(filePath);
    }

    // ── Row style helpers ─────────────────────────────────────────────────

    public static ExcelRowStyle GetAdvisorRowStyle(Advisor a)
    {
        if (a.IsExcluded) return ExcelRowStyle.Excluded;
        if (a.HasDisclosures) return ExcelRowStyle.Disclosure;
        if (a.IsFavorited) return ExcelRowStyle.Favorited;
        return ExcelRowStyle.Normal;
    }

    public static ExcelRowStyle GetFirmRowStyle(Firm f)
    {
        if (f.IsExcluded) return ExcelRowStyle.Excluded;
        return ExcelRowStyle.Normal;
    }

    // ── CSV escaping ──────────────────────────────────────────────────────

    private static string CsvEscape(string? value)
    {
        if (value == null) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
