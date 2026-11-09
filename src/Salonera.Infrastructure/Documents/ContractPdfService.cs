using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Salonera.Application.Features.Sales;
using Salonera.Domain.Enums;

namespace Salonera.Infrastructure.Documents;

public sealed class ContractPdfService(ISalesContractService contractService) : IContractPdfService
{
    private static readonly Lock FontLock = new();
    private static bool _fontResolverConfigured;
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public async Task<string> GenerateAsync(
        Guid contractId,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var document = await contractService.GetContractDocumentAsync(contractId, cancellationToken);
        await RenderAsync(document, outputPath, cancellationToken);
        return Path.GetFullPath(outputPath);
    }

    public Task RenderAsync(
        ContractDocument document,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureFontResolver();
        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        using var pdf = new PdfDocument();
        pdf.Info.Title = $"Salonera Sözleşme - {document.ContractNumber}";
        pdf.Info.Author = document.BranchName;
        var page = pdf.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        using var graphics = XGraphics.FromPdfPage(page);
        DrawContract(graphics, page, document);
        pdf.Save(fullPath);
        return Task.CompletedTask;
    }

    private static void DrawContract(XGraphics graphics, PdfPage page, ContractDocument document)
    {
        var width = page.Width.Point;
        var regular = new XFont("Salonera Sans", 9.5, XFontStyleEx.Regular);
        var small = new XFont("Salonera Sans", 8, XFontStyleEx.Regular);
        var bold = new XFont("Salonera Sans", 9.5, XFontStyleEx.Bold);
        var title = new XFont("Salonera Sans", 20, XFontStyleEx.Bold);
        var section = new XFont("Salonera Sans", 11, XFontStyleEx.Bold);
        var blue = XColor.FromArgb(29, 78, 216);
        var dark = XColor.FromArgb(23, 32, 51);
        const double margin = 42;
        var contentWidth = width - (margin * 2);

        graphics.DrawRectangle(new XSolidBrush(dark), 0, 0, width, 92);
        graphics.DrawString("SALONERA", title, XBrushes.White, new XRect(margin, 24, 260, 30), XStringFormats.TopLeft);
        graphics.DrawString("Salon Yönetimi ve Ön Muhasebe", regular, new XSolidBrush(XColor.FromArgb(203, 213, 225)),
            new XRect(margin, 56, 300, 20), XStringFormats.TopLeft);
        graphics.DrawString("ORGANİZASYON SÖZLEŞMESİ", section, XBrushes.White,
            new XRect(width - margin - 230, 32, 230, 25), XStringFormats.TopRight);

        var y = 112d;
        DrawLabelValue(graphics, margin, y, "Sözleşme No", document.ContractNumber, regular, bold);
        DrawLabelValue(graphics, width / 2, y, "Sözleşme Tarihi", document.ContractDate.ToString("dd.MM.yyyy", TurkishCulture), regular, bold);
        y += 35;
        DrawSectionHeader(graphics, margin, y, contentWidth, "MÜŞTERİ VE ORGANİZASYON BİLGİLERİ", section, blue);
        y += 30;
        var infoColumnWidth = contentWidth * .46;
        var secondColumnX = margin + contentWidth * .54;
        DrawInfoCell(graphics, margin, y, infoColumnWidth, "Müşteri", document.CustomerName, regular, bold);
        DrawInfoCell(graphics, secondColumnX, y, infoColumnWidth, "Telefon", document.CustomerPhone, regular, bold);
        y += 40;
        DrawInfoCell(graphics, margin, y, infoColumnWidth, "Şube / Salon", $"{document.BranchName} / {document.HallName}", regular, bold);
        DrawInfoCell(graphics, secondColumnX, y, infoColumnWidth, "Organizasyon Türü", document.EventTypeName, regular, bold);
        y += 40;
        DrawInfoCell(graphics, margin, y, infoColumnWidth, "Tarih ve Saat",
            $"{document.EventStartsAt.ToString("dd.MM.yyyy HH:mm", TurkishCulture)} - {document.EventEndsAt.ToString("HH:mm", TurkishCulture)}", regular, bold);
        DrawInfoCell(graphics, secondColumnX, y, infoColumnWidth, "Davetli Sayısı", document.GuestCount.ToString(TurkishCulture), regular, bold);
        y += 50;

        DrawSectionHeader(graphics, margin, y, contentWidth, "PAKET VE HİZMETLER", section, blue);
        y += 30;
        DrawTableHeader(graphics, margin, y, contentWidth, bold);
        y += 24;
        foreach (var line in document.Lines)
        {
            graphics.DrawString(line.Description, regular, XBrushes.Black, new XRect(margin + 7, y, contentWidth * .49, 18), XStringFormats.TopLeft);
            graphics.DrawString(line.Quantity.ToString("N2", TurkishCulture), regular, XBrushes.Black,
                new XRect(margin + contentWidth * .53, y, contentWidth * .1, 18), XStringFormats.TopRight);
            graphics.DrawString(Money(line.UnitPrice, document.Totals.Currency), regular, XBrushes.Black,
                new XRect(margin + contentWidth * .65, y, contentWidth * .16, 18), XStringFormats.TopRight);
            graphics.DrawString(Money(line.TotalPrice, document.Totals.Currency), regular, XBrushes.Black,
                new XRect(margin + contentWidth * .82, y, contentWidth * .17, 18), XStringFormats.TopRight);
            graphics.DrawLine(new XPen(XColor.FromArgb(226, 232, 240)), margin, y + 20, margin + contentWidth, y + 20);
            y += 25;
        }

        y += 10;
        var totalsX = margin + contentWidth * .55;
        DrawTotal(graphics, totalsX, y, contentWidth * .45, "Brüt Tutar", Money(document.Totals.GrossAmount, document.Totals.Currency), regular, bold);
        y += 22;
        DrawTotal(graphics, totalsX, y, contentWidth * .45, "İndirim", Money(document.Totals.DiscountAmount, document.Totals.Currency), regular, bold);
        y += 22;
        DrawTotal(graphics, totalsX, y, contentWidth * .45,
            $"KDV (%{document.Totals.VatRate.ToString("N2", TurkishCulture)})",
            Money(document.Totals.VatAmount, document.Totals.Currency), regular, bold);
        y += 25;
        graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(219, 234, 254)), totalsX, y, contentWidth * .45, 32);
        DrawTotal(graphics, totalsX + 7, y + 8, contentWidth * .45 - 14, "Net Sözleşme", Money(document.Totals.NetAmount, document.Totals.Currency), bold, bold);
        y += 45;
        if (document.Totals.Currency != CurrencyCode.Try)
        {
            DrawTotal(graphics, totalsX, y, contentWidth * .45,
                $"Kur: {document.Totals.ExchangeRate.ToString("N4", TurkishCulture)}",
                $"{document.Totals.NetAmountTry.ToString("N2", TurkishCulture)} TRY", regular, bold);
            y += 28;
        }

        var termsY = Math.Max(y + 15, 520);
        DrawSectionHeader(graphics, margin, termsY, contentWidth, "GENEL ŞARTLAR", section, blue);
        var formatter = new XTextFormatter(graphics);
        formatter.DrawString(
            "Bu belge Salonera ön muhasebe sistemi tarafından hazırlanmıştır. Taraflar; organizasyon tarihi, salon, hizmetler ve mali tutarların doğruluğunu kontrol ederek kabul eder. Ödeme planı ve özel koşullar ayrıca düzenlenir.",
            small,
            XBrushes.Black,
            new XRect(margin, termsY + 28, contentWidth, 48));

        var signatureY = termsY + 96;
        DrawSignature(graphics, margin, signatureY, 200, "Müşteri");
        DrawSignature(graphics, width - margin - 200, signatureY, 200, "İşletme Yetkilisi");
        graphics.DrawString("Salonera - Ön muhasebe uygulamasıdır; resmî muhasebe belgesi değildir.", small,
            new XSolidBrush(XColor.FromArgb(100, 116, 139)), new XRect(margin, page.Height.Point - 35, contentWidth, 15), XStringFormats.TopCenter);
    }

    private static void DrawSectionHeader(XGraphics graphics, double x, double y, double width, string text, XFont font, XColor color)
    {
        graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(239, 246, 255)), x, y, width, 23);
        graphics.DrawRectangle(new XSolidBrush(color), x, y, 4, 23);
        graphics.DrawString(text, font, new XSolidBrush(color), new XRect(x + 11, y + 5, width - 15, 16), XStringFormats.TopLeft);
    }

    private static void DrawLabelValue(XGraphics graphics, double x, double y, string label, string value, XFont regular, XFont bold)
    {
        graphics.DrawString(label + ":", regular, new XSolidBrush(XColor.FromArgb(100, 116, 139)), new XRect(x, y, 105, 18), XStringFormats.TopLeft);
        graphics.DrawString(value, bold, XBrushes.Black, new XRect(x + 108, y, 220, 18), XStringFormats.TopLeft);
    }

    private static void DrawInfoCell(
        XGraphics graphics,
        double x,
        double y,
        double width,
        string label,
        string value,
        XFont regular,
        XFont bold)
    {
        graphics.DrawString(label, regular, new XSolidBrush(XColor.FromArgb(100, 116, 139)),
            new XRect(x, y, width, 15), XStringFormats.TopLeft);
        graphics.DrawString(value, bold, XBrushes.Black,
            new XRect(x, y + 16, width, 18), XStringFormats.TopLeft);
    }

    private static void DrawTableHeader(XGraphics graphics, double x, double y, double width, XFont font)
    {
        graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(241, 245, 249)), x, y, width, 22);
        graphics.DrawString("Açıklama", font, XBrushes.Black, new XRect(x + 7, y + 5, width * .48, 15), XStringFormats.TopLeft);
        graphics.DrawString("Adet", font, XBrushes.Black, new XRect(x + width * .53, y + 5, width * .1, 15), XStringFormats.TopRight);
        graphics.DrawString("Birim", font, XBrushes.Black, new XRect(x + width * .65, y + 5, width * .16, 15), XStringFormats.TopRight);
        graphics.DrawString("Toplam", font, XBrushes.Black, new XRect(x + width * .82, y + 5, width * .17, 15), XStringFormats.TopRight);
    }

    private static void DrawTotal(XGraphics graphics, double x, double y, double width, string label, string value, XFont regular, XFont bold)
    {
        graphics.DrawString(label, regular, XBrushes.Black, new XRect(x, y, width * .55, 18), XStringFormats.TopLeft);
        graphics.DrawString(value, bold, XBrushes.Black, new XRect(x + width * .55, y, width * .45, 18), XStringFormats.TopRight);
    }

    private static void DrawSignature(XGraphics graphics, double x, double y, double width, string title)
    {
        graphics.DrawLine(new XPen(XColor.FromArgb(100, 116, 139)), x, y + 42, x + width, y + 42);
        graphics.DrawString(title, new XFont("Salonera Sans", 8, XFontStyleEx.Regular), XBrushes.Black,
            new XRect(x, y + 48, width, 14), XStringFormats.TopCenter);
    }

    private static string Money(decimal amount, CurrencyCode currency)
        => $"{amount.ToString("N2", TurkishCulture)} {currency.ToString().ToUpperInvariant()}";

    private static void EnsureFontResolver()
    {
        lock (FontLock)
        {
            if (_fontResolverConfigured)
            {
                return;
            }

            GlobalFontSettings.FontResolver = new SystemFontResolver();
            _fontResolverConfigured = true;
        }
    }
}
