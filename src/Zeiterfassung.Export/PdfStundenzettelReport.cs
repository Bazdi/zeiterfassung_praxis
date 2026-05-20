using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Zeiterfassung.Export;

public class PdfStundenzettelReport : IDocument
{
    private readonly MonthReport _report;

    public PdfStundenzettelReport(MonthReport report)
    {
        _report = report;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(_report.PracticeName).FontSize(14).Bold();
                    left.Item().Text("Stundennachweis").FontSize(11).FontColor(Colors.Grey.Medium);
                });
                row.ConstantItem(200).AlignRight().Column(right =>
                {
                    right.Item().Text($"{_report.EmployeeName}").FontSize(11).Bold().AlignRight();
                    right.Item().Text(MonthName()).FontSize(10).FontColor(Colors.Grey.Medium).AlignRight();
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingTop(16).Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(80);  // Datum
                    cols.ConstantColumn(50);  // Kommen
                    cols.ConstantColumn(50);  // Gehen
                    cols.ConstantColumn(40);  // Pause
                    cols.ConstantColumn(50);  // Ist
                    cols.ConstantColumn(50);  // Soll
                    cols.RelativeColumn();    // Diff
                });

                table.Header(header =>
                {
                    static void HeaderCell(IContainer c, string text) =>
                        c.Background(Colors.Grey.Lighten3)
                         .Padding(4)
                         .Text(text).Bold().FontSize(8);

                    header.Cell().Element(c => HeaderCell(c, "Datum"));
                    header.Cell().Element(c => HeaderCell(c, "Kommen"));
                    header.Cell().Element(c => HeaderCell(c, "Gehen"));
                    header.Cell().Element(c => HeaderCell(c, "Pause"));
                    header.Cell().Element(c => HeaderCell(c, "Ist (h)"));
                    header.Cell().Element(c => HeaderCell(c, "Soll (h)"));
                    header.Cell().Element(c => HeaderCell(c, "Diff (h)"));
                });

                foreach (var day in _report.Days)
                {
                    bool isWeekend = day.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                    var bg = isWeekend || day.IsHoliday ? Colors.Grey.Lighten4 : Colors.White;

                    void DataCell(IContainer c, string text, bool right = false, bool bold = false, string? color = null) =>
                        c.Background(bg)
                         .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                         .Padding(3)
                         .Text(t =>
                         {
                             t.Span(text);
                             if (bold) t.Span("").Bold();
                         });

                    var diffText = day.WorkedHours > 0 || day.RequiredHours > 0
                        ? $"{(day.BalanceHours >= 0 ? "+" : "")}{day.BalanceHours:F2}"
                        : "";
                    var diffColor = day.BalanceHours >= 0 ? Colors.Green.Darken1 : Colors.Red.Darken1;

                    table.Cell().Element(c =>
                        c.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                         .Padding(3).Text(day.Date.ToString("ddd dd.MM")));
                    table.Cell().Element(c =>
                        c.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                         .Padding(3).Text(day.KommenLocal?.ToString("HH:mm") ?? (day.IsHoliday ? day.HolidayName ?? "Feiertag" : "")));
                    table.Cell().Element(c =>
                        c.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                         .Padding(3).Text(day.GehenLocal?.ToString("HH:mm") ?? ""));
                    table.Cell().Element(c =>
                        c.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                         .Padding(3).Text(day.PauseMinutes > 0 ? $"{day.PauseMinutes}m" : ""));
                    table.Cell().Element(c =>
                        c.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                         .Padding(3).Text(day.WorkedHours > 0 ? day.WorkedHours.ToString("F2") : ""));
                    table.Cell().Element(c =>
                        c.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                         .Padding(3).Text(day.RequiredHours > 0 ? day.RequiredHours.ToString("F2") : ""));
                    table.Cell().Element(c =>
                        c.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                         .Padding(3).Text(t => t.Span(diffText).FontColor(diffColor)));
                }

                // Totals row
                static void TotalCell(IContainer c, string text) =>
                    c.Background(Colors.Grey.Lighten3)
                     .Padding(4)
                     .Text(text).Bold();

                var balSign = _report.TotalBalance >= 0 ? "+" : "";
                table.Cell().Element(c => TotalCell(c, "Gesamt"));
                table.Cell().Element(c => TotalCell(c, ""));
                table.Cell().Element(c => TotalCell(c, ""));
                table.Cell().Element(c => TotalCell(c, ""));
                table.Cell().Element(c => TotalCell(c, _report.TotalWorkedHours.ToString("F2")));
                table.Cell().Element(c => TotalCell(c, _report.TotalRequiredHours.ToString("F2")));
                table.Cell().Element(c => TotalCell(c, $"{balSign}{_report.TotalBalance:F2}"));
            });

            // Signature section
            col.Item().PaddingTop(40).Row(row =>
            {
                row.RelativeItem().Column(sig =>
                {
                    sig.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                    sig.Item().PaddingTop(4).Text("Datum, Unterschrift Mitarbeiter").FontSize(8).FontColor(Colors.Grey.Medium);
                });
                row.ConstantItem(40);
                row.RelativeItem().Column(sig =>
                {
                    sig.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                    sig.Item().PaddingTop(4).Text("Datum, Unterschrift Praxisinhaber").FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text(t =>
            {
                t.Span("GoBD-konformes Zeiterfassungssystem — ").FontSize(7).FontColor(Colors.Grey.Lighten1);
                t.Span($"Erstellt: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(7).FontColor(Colors.Grey.Lighten1);
            });
            row.ConstantItem(50).AlignRight().Text(t =>
            {
                t.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Lighten1);
                t.Span(" / ").FontSize(7).FontColor(Colors.Grey.Lighten1);
                t.TotalPages().FontSize(7).FontColor(Colors.Grey.Lighten1);
            });
        });
    }

    private string MonthName() =>
        new DateTime(_report.Year, _report.Month, 1).ToString("MMMM yyyy");

    public static byte[] Generate(MonthReport report)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return new PdfStundenzettelReport(report).GeneratePdf();
    }
}
