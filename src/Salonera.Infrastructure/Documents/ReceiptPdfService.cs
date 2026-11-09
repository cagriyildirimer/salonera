using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Salonera.Application.Features.Payments;

namespace Salonera.Infrastructure.Documents;

public sealed class ReceiptPdfService(IPaymentService payments) : IReceiptPdfService
{
    public async Task<string> GenerateAsync(Guid receiptId, string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var document = await payments.GetReceiptAsync(receiptId, cancellationToken);
        var balance = document.Balance;
        var receipt = document.Receipt;
        ContractPdfService.EnsureFontResolver();
        var path = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var pdf = new PdfDocument();
        pdf.Info.Title = $"Tahsilat Makbuzu {receipt.ReceiptNumber}";
        var page = pdf.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        using var graphics = XGraphics.FromPdfPage(page);
        var title = new XFont("Salonera Sans", 20, XFontStyleEx.Bold);
        var regular = new XFont("Salonera Sans", 11, XFontStyleEx.Regular);
        var bold = new XFont("Salonera Sans", 11, XFontStyleEx.Bold);
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(23, 32, 51)), 0, 0, page.Width.Point, 100);
        graphics.DrawString("SALONERA", title, XBrushes.White, 42, 48);
        graphics.DrawString("TAHSİLAT MAKBUZU", bold, XBrushes.White, page.Width.Point - 190, 48);
        var lines = new (string Label, string Value)[]
        {
            ("Makbuz No", receipt.ReceiptNumber),
            ("Tarih", receipt.CollectedAt.ToOffset(TimeSpan.FromHours(3)).ToString("dd.MM.yyyy HH:mm", culture)),
            ("Sözleşme No", balance.ContractNumber),
            ("Müşteri", balance.CustomerName),
            ("Ödeme Yöntemi", receipt.MethodText),
            ("Tahsilat Tutarı", $"{receipt.Amount.ToString("N2", culture)} {receipt.Currency.ToString().ToUpperInvariant()}"),
            ("İade Edilen", $"{receipt.Refunded.ToString("N2", culture)} {receipt.Currency.ToString().ToUpperInvariant()}"),
            ("Sözleşmede Kalan", $"{balance.Remaining.ToString("N2", culture)} {balance.Currency.ToString().ToUpperInvariant()}"),
            ("Durum", receipt.IsCancelled ? "İPTAL EDİLDİ" : "Geçerli"),
        };
        var y = 150d;
        foreach (var line in lines)
        {
            graphics.DrawString(line.Label, regular, XBrushes.Gray, new XRect(45, y, 170, 22), XStringFormats.TopLeft);
            graphics.DrawString(line.Value, bold, XBrushes.Black, new XRect(215, y, 325, 22), XStringFormats.TopLeft);
            graphics.DrawLine(XPens.LightGray, 45, y + 25, page.Width.Point - 45, y + 25);
            y += 43;
        }
        graphics.DrawString("Salonera ön muhasebe kaydıdır; resmî mali belge değildir.", regular,
            XBrushes.Gray, new XRect(45, page.Height.Point - 65, page.Width.Point - 90, 20), XStringFormats.TopLeft);
        pdf.Save(path);
        return path;
    }

}
