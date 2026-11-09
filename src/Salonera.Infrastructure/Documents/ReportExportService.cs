using System.IO.Compression;
using System.Globalization;
using System.Text;
using System.Xml;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using Salonera.Application.Features.Reports;

namespace Salonera.Infrastructure.Documents;

public sealed class ReportExportService : IReportExportService
{
    public async Task<string> ExportAsync(ReportTable report, string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        cancellationToken.ThrowIfCancellationRequested();
        var path = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".csv":
                await WriteCsvAsync(report, path, cancellationToken);
                break;
            case ".xlsx":
                WriteExcel(report, path);
                break;
            case ".pdf":
                WritePdf(report, path);
                break;
            default:
                throw new ArgumentException("Desteklenen rapor türleri: CSV, XLSX ve PDF.", nameof(outputPath));
        }
        return path;
    }

    private static async Task WriteCsvAsync(ReportTable report, string path, CancellationToken ct)
    {
        static string Line(IEnumerable<string> cells) => string.Join(';', cells.Select(cell =>
            '"' + cell.Replace("\"", "\"\"", StringComparison.Ordinal) + '"'));
        var lines = new[] { Line(report.Columns) }.Concat(report.Rows.Select(row => Line(row.Cells)))
            .Concat([string.Empty, Line([report.Note])]);
        await File.WriteAllLinesAsync(path, lines, new UTF8Encoding(true), ct);
    }

    private static void WriteExcel(ReportTable report, string path)
    {
        using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        WriteEntry(archive, "[Content_Types].xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
            </Types>
            """);
        WriteEntry(archive, "_rels/.rels", """
            <?xml version="1.0" encoding="utf-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);
        WriteEntry(archive, "xl/workbook.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets><sheet name="Rapor" sheetId="1" r:id="rId1"/></sheets>
            </workbook>
            """);
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="utf-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
            </Relationships>
            """);
        var entry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        writer.WriteStartElement("cols");
        for (var i = 0; i < report.Columns.Count; i++)
        {
            writer.WriteStartElement("col");
            writer.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("width", "22");
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData");
        var allRows = new[] { report.Columns }.Concat(report.Rows.Select(row => row.Cells))
            .Concat([new[] { report.Note }]);
        var rowNumber = 1;
        foreach (var cells in allRows)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var column = 0;
            foreach (var value in cells)
            {
                var index = column++;
                writer.WriteStartElement("c");
                writer.WriteAttributeString("r", ColumnName(index) + rowNumber);
                if (rowNumber > 1 && IsMoneyColumn(report.Columns[index]) &&
                    decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out var amount))
                {
                    writer.WriteElementString("v", amount.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    writer.WriteAttributeString("t", "inlineStr");
                    writer.WriteStartElement("is");
                    writer.WriteElementString("t", value);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            rowNumber++;
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static bool IsMoneyColumn(string header) =>
        header.Contains("(TRY)", StringComparison.Ordinal) ||
        header is "Açılış" or "Giriş" or "Çıkış" or "Kapanış";

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        for (index++; index > 0; index = (index - 1) / 26)
            name = (char)('A' + (index - 1) % 26) + name;
        return name;
    }

    private static void WriteEntry(ZipArchive archive, string name, string contents)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(contents);
    }

    private static void WritePdf(ReportTable report, string path)
    {
        ContractPdfService.EnsureFontResolver();
        using var document = new PdfDocument();
        var titleFont = new XFont("Salonera Sans", 15, XFontStyleEx.Bold);
        var bodyFont = new XFont("Salonera Sans", 7, XFontStyleEx.Regular);
        var headerFont = new XFont("Salonera Sans", 7, XFontStyleEx.Bold);
        PdfPage? page = null;
        XGraphics? graphics = null;
        var y = 0d;
        void NewPage()
        {
            graphics?.Dispose();
            page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            page.Orientation = PdfSharp.PageOrientation.Landscape;
            graphics = XGraphics.FromPdfPage(page);
            graphics.DrawString(report.Title, titleFont, XBrushes.Black, 30, 32);
            new XTextFormatter(graphics).DrawString(report.Note, bodyFont, XBrushes.Gray,
                new XRect(30, 40, page.Width.Point - 60, 34));
            y = 78;
            DrawRow(report.Columns, headerFont, true);
        }
        void DrawRow(IReadOnlyList<string> cells, XFont font, bool header)
        {
            var width = (page!.Width.Point - 60) / report.Columns.Count;
            if (header) graphics!.DrawRectangle(XBrushes.LightGray, 30, y, page.Width.Point - 60, 24);
            for (var i = 0; i < cells.Count; i++)
            {
                var value = cells[i];
                while (value.Length > 1 && graphics!.MeasureString(value, font).Width > width - 8)
                    value = value[..^1];
                graphics!.DrawString(value, font, XBrushes.Black,
                    new XRect(34 + i * width, y + 5, width - 8, 17), XStringFormats.TopLeft);
            }
            graphics!.DrawLine(XPens.LightGray, 30, y + 24, page.Width.Point - 30, y + 24);
            y += 24;
        }
        NewPage();
        foreach (var row in report.Rows)
        {
            if (y + 44 > page!.Height.Point) NewPage();
            DrawRow(row.Cells, bodyFont, false);
        }
        graphics!.Dispose();
        document.Save(path);
    }
}
