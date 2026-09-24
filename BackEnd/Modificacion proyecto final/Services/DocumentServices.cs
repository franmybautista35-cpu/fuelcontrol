using ClosedXML.Excel;
using FuelTickets.Models;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FuelTickets.Services;

public static class DocumentServices
{
    public static byte[] QrPng(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(12);
    }

    public static byte[] TicketPdf(FuelTicket t, string payload)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var qr = QrPng(payload);
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(11));
            page.Header().Text("TICKET DIGITAL DE COMBUSTIBLE").Bold().FontSize(17).FontColor(Colors.Blue.Darken2);
            page.Content().Column(c =>
            {
                c.Spacing(8);
                c.Item().Text($"Número: {t.Sequence}").Bold();
                c.Item().Text($"Empleado: {t.Request?.Employee?.FullName}");
                c.Item().Text($"Vehículo: {t.Request?.Vehicle?.Plate} - {t.Request?.Vehicle?.Make} {t.Request?.Vehicle?.Model}");
                c.Item().Text($"Departamento: {t.Request?.Department?.Name}");
                c.Item().Text($"Combustible: {t.Request?.FuelType}");
                c.Item().Text($"Cantidad autorizada: {t.Request?.AuthorizedGallons:N2} galones");
                c.Item().Text($"Válido hasta: {t.ExpiresAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}");
                c.Item().PaddingTop(10).AlignCenter().Width(190).Image(qr);
                c.Item().AlignCenter().Text("Presente este QR en la estación. Uso único.").FontSize(9);
            });
            page.Footer().AlignCenter().Text(x => { x.Span("UUID: "); x.Span(t.Id.ToString()).FontSize(8); });
        })).GeneratePdf();
    }

    public static byte[] ReportExcel(IEnumerable<Dispatch> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Despachos");
        string[] headers = ["Fecha", "Ticket", "Empleado", "Vehículo", "Departamento", "Combustible", "Galones", "Operador", "Estación"];
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        var row = 2;
        foreach (var x in rows)
        {
            ws.Cell(row, 1).Value = x.DispatchedAtUtc;
            ws.Cell(row, 2).Value = x.Ticket?.Sequence;
            ws.Cell(row, 3).Value = x.Ticket?.Request?.Employee?.FullName;
            ws.Cell(row, 4).Value = x.Ticket?.Request?.Vehicle?.Plate;
            ws.Cell(row, 5).Value = x.Ticket?.Request?.Department?.Name;
            ws.Cell(row, 6).Value = x.Ticket?.Request?.FuelType;
            ws.Cell(row, 7).Value = x.GallonsServed;
            ws.Cell(row, 8).Value = x.OperatorUser?.FullName;
            ws.Cell(row, 9).Value = x.Station;
            row++;
        }
        var table = ws.Range(1, 1, Math.Max(2, row - 1), headers.Length).CreateTable();
        table.Theme = XLTableTheme.TableStyleMedium2;
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] ReportPdf(IReadOnlyCollection<Dispatch> rows)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.Letter.Landscape()); page.Margin(24); page.DefaultTextStyle(x=>x.FontSize(8));
            page.Header().Column(c=>{c.Item().Text("REPORTE DE DESPACHOS DE COMBUSTIBLE").Bold().FontSize(15).FontColor(Colors.Blue.Darken2);c.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm} · Registros: {rows.Count}");});
            page.Content().PaddingTop(12).Table(t=>
            {
                t.ColumnsDefinition(c=>{c.ConstantColumn(88);c.ConstantColumn(90);c.RelativeColumn();c.ConstantColumn(70);c.RelativeColumn();c.ConstantColumn(55);c.RelativeColumn();});
                t.Header(h=>{foreach(var s in new[]{"Fecha","Ticket","Empleado","Vehículo","Departamento","Galones","Estación"})h.Cell().Background(Colors.Blue.Darken2).Padding(5).Text(s).FontColor(Colors.White).Bold();});
                foreach(var x in rows)
                {
                    string[] cells=[x.DispatchedAtUtc.ToLocalTime().ToString("dd/MM/yy HH:mm"),x.Ticket?.Sequence??"",x.Ticket?.Request?.Employee?.FullName??"",x.Ticket?.Request?.Vehicle?.Plate??"",x.Ticket?.Request?.Department?.Name??"",x.GallonsServed.ToString("N2"),x.Station];
                    foreach(var s in cells)t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(s);
                }
            });
            page.Footer().AlignRight().Text(x=>{x.Span("Página ");x.CurrentPageNumber();x.Span(" de ");x.TotalPages();});
        })).GeneratePdf();
    }
}
