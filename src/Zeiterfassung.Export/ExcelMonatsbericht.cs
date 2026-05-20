using ClosedXML.Excel;

namespace Zeiterfassung.Export;

public static class ExcelMonatsbericht
{
    public static byte[] Generate(IList<MonthReport> reports)
    {
        using var wb = new XLWorkbook();

        // Summary sheet
        var summary = wb.Worksheets.Add("Übersicht");
        StyleSummary(summary, reports);

        // One sheet per employee
        foreach (var report in reports)
        {
            var sheetName = report.EmployeeName.Length > 31
                ? report.EmployeeName[..31]
                : report.EmployeeName;
            var ws = wb.Worksheets.Add(sheetName);
            StyleEmployee(ws, report);
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void StyleSummary(IXLWorksheet ws, IList<MonthReport> reports)
    {
        if (!reports.Any()) return;

        var monthLabel = new DateTime(reports[0].Year, reports[0].Month, 1).ToString("MMMM yyyy");

        ws.Cell("A1").Value = $"Übersicht Zeiterfassung — {monthLabel}";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;

        var headers = new[] { "Mitarbeiter", "Ist (h)", "Soll (h)", "Saldo (h)" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(3, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x22, 0x26, 0x3A);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        int row = 4;
        foreach (var r in reports)
        {
            ws.Cell(row, 1).Value = r.EmployeeName;
            ws.Cell(row, 2).Value = (double)r.TotalWorkedHours;
            ws.Cell(row, 3).Value = (double)r.TotalRequiredHours;
            ws.Cell(row, 4).Value = (double)r.TotalBalance;

            ws.Cell(row, 2).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.00";
            ws.Cell(row, 4).Style.NumberFormat.Format = "+0.00;-0.00";

            var balCell = ws.Cell(row, 4);
            if (r.TotalBalance >= 0)
                balCell.Style.Font.FontColor = XLColor.FromArgb(0x22, 0xc5, 0x5e);
            else
                balCell.Style.Font.FontColor = XLColor.FromArgb(0xdc, 0x26, 0x26);

            row++;
        }

        // Totals
        ws.Cell(row, 1).Value = "Gesamt";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 2).Value = (double)reports.Sum(r => r.TotalWorkedHours);
        ws.Cell(row, 3).Value = (double)reports.Sum(r => r.TotalRequiredHours);
        ws.Cell(row, 4).Value = (double)reports.Sum(r => r.TotalBalance);
        for (int c = 1; c <= 4; c++)
        {
            ws.Cell(row, c).Style.Font.Bold = true;
            ws.Cell(row, c).Style.NumberFormat.Format = c > 1 ? "0.00" : "";
        }

        ws.Column(1).Width = 30;
        ws.Columns(2, 4).Width = 14;
        ws.Cell("A3").Style.Alignment.WrapText = false;
    }

    private static void StyleEmployee(IXLWorksheet ws, MonthReport report)
    {
        var monthLabel = new DateTime(report.Year, report.Month, 1).ToString("MMMM yyyy");

        ws.Cell("A1").Value = $"{report.EmployeeName} — {monthLabel}";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 12;

        var headers = new[] { "Datum", "Tag", "Kommen", "Gehen", "Pause (min)", "Ist (h)", "Soll (h)", "Differenz (h)", "Hinweis" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(3, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x22, 0x26, 0x3A);
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row = 4;
        foreach (var day in report.Days)
        {
            bool weekend = day.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            bool special = weekend || day.IsHoliday;

            ws.Cell(row, 1).Value = day.Date.ToString("dd.MM.yyyy");
            ws.Cell(row, 2).Value = day.Date.ToString("ddd");
            ws.Cell(row, 3).Value = day.KommenLocal?.ToString("HH:mm") ?? "";
            ws.Cell(row, 4).Value = day.GehenLocal?.ToString("HH:mm") ?? "";
            ws.Cell(row, 5).Value = day.PauseMinutes;
            if (day.WorkedHours > 0) ws.Cell(row, 6).Value = (double)day.WorkedHours;
            if (day.RequiredHours > 0) ws.Cell(row, 7).Value = (double)day.RequiredHours;
            if (day.WorkedHours > 0 || day.RequiredHours > 0)
                ws.Cell(row, 8).Value = (double)day.BalanceHours;
            ws.Cell(row, 9).Value = day.IsHoliday ? day.HolidayName ?? "Feiertag"
                : day.HasWarning ? day.WarningText ?? "Warnung" : "";

            if (ws.Cell(row, 6).Value is double) ws.Cell(row, 6).Style.NumberFormat.Format = "0.00";
            if (ws.Cell(row, 7).Value is double) ws.Cell(row, 7).Style.NumberFormat.Format = "0.00";
            if (ws.Cell(row, 8).Value is double)
            {
                ws.Cell(row, 8).Style.NumberFormat.Format = "+0.00;-0.00";
                var bal = day.BalanceHours;
                ws.Cell(row, 8).Style.Font.FontColor = bal >= 0
                    ? XLColor.FromArgb(0x22, 0xc5, 0x5e)
                    : XLColor.FromArgb(0xdc, 0x26, 0x26);
            }

            if (special)
            {
                var range = ws.Range(row, 1, row, 9);
                range.Style.Fill.BackgroundColor = XLColor.FromArgb(0xF3, 0xF4, 0xF6);
            }

            if (day.HasWarning)
                ws.Cell(row, 9).Style.Font.FontColor = XLColor.FromArgb(0xdc, 0x26, 0x26);

            row++;
        }

        // Totals
        ws.Cell(row, 1).Value = "Gesamt";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 6).Value = (double)report.TotalWorkedHours;
        ws.Cell(row, 7).Value = (double)report.TotalRequiredHours;
        ws.Cell(row, 8).Value = (double)report.TotalBalance;
        for (int c = 6; c <= 8; c++)
        {
            ws.Cell(row, c).Style.Font.Bold = true;
            ws.Cell(row, c).Style.NumberFormat.Format = c == 8 ? "+0.00;-0.00" : "0.00";
        }
        ws.Cell(row, 8).Style.Font.FontColor = report.TotalBalance >= 0
            ? XLColor.FromArgb(0x22, 0xc5, 0x5e)
            : XLColor.FromArgb(0xdc, 0x26, 0x26);

        ws.Column(1).Width = 14;
        ws.Column(2).Width = 6;
        ws.Columns(3, 4).Width = 10;
        ws.Columns(6, 8).Width = 12;
        ws.Column(9).Width = 25;
    }
}
