using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using LIS.Models;

namespace LIS.Services;

public class PdfExportService
{
    public byte[] GenerateReportPdf(Report report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));

                // Header
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("LABORATORY REPORT").FontSize(20).SemiBold().FontColor("#6fd67f");
                        col.Item().Text(report.Hospital?.Name ?? "Geneflux Diagnostics").FontSize(12).SemiBold();
                        col.Item().Text(report.Hospital?.Address ?? "").FontSize(9).FontColor(Colors.Grey.Medium);
                    });

                    row.ConstantItem(100).Column(col =>
                    {
                        col.Item().Text($"Ref: {report.ReferenceNumber}").FontSize(9).AlignRight();
                        col.Item().Text($"Date: {report.ReportingDate?.ToString("dd/MM/yyyy") ?? DateTime.Now.ToString("dd/MM/yyyy")}").FontSize(9).AlignRight();
                    });
                });

                // Content
                page.Content().PaddingVertical(20).Column(col =>
                {
                    // Patient Info Box
                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(t => { t.Span("Patient Name: ").SemiBold(); t.Span(report.Patient?.Name ?? "N/A"); });
                            c.Item().Text(t => { t.Span("NRIC/Passport: ").SemiBold(); t.Span(report.Patient?.IdentityNumber ?? "N/A"); });
                            c.Item().Text(t => { t.Span("MRN: ").SemiBold(); t.Span(report.Patient?.MRN ?? "N/A"); });
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(t => { t.Span("Sex: ").SemiBold(); t.Span(report.Patient?.Sex.ToString() ?? "N/A"); });
                            c.Item().Text(t => { t.Span("Doctor: ").SemiBold(); t.Span(report.Doctor?.Name ?? "N/A"); });
                            c.Item().Text(t => { t.Span("Test Name: ").SemiBold(); t.Span(report.Test?.Name ?? "N/A"); });
                        });
                    });

                    col.Item().PaddingTop(20).Text("TEST RESULTS").SemiBold().FontSize(12).Underline();

                    // Results Table
                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Test");
                            header.Cell().Element(CellStyle).Text("Result");
                            header.Cell().Element(CellStyle).Text("Flag/Details");

                            static IContainer CellStyle(IContainer container)
                            {
                                return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                            }
                        });

                        foreach (var result in report.TestResults.OrderBy(r => r.SortOrder))
                        {
                            table.Cell().Element(ValueStyle).Text(result.TestName);
                            table.Cell().Element(ValueStyle).Text(result.Result);
                            table.Cell().Element(ValueStyle).Text(result.ResultDetail ?? "");

                            static IContainer ValueStyle(IContainer container)
                            {
                                return container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten3);
                            }
                        }
                    });

                    // Footer content
                    col.Item().PaddingTop(40).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Specimen: " + (report.SpecimenType ?? "N/A"));
                            c.Item().Text("Collected: " + (report.SampleCollectionDate?.ToString("dd/MM/yyyy") ?? "N/A"));
                            c.Item().Text("Received: " + (report.ReceivedAtLabDate?.ToString("dd/MM/yyyy") ?? "N/A"));
                        });

                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().PaddingTop(20).BorderTop(1).Width(150).AlignCenter().Text("Pathologist Signature").FontSize(8);
                        });
                    });
                });

                // Footer
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}
