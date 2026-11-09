using PdfSharp.Fonts;

namespace Salonera.Infrastructure.Documents;

internal sealed class SystemFontResolver : IFontResolver
{
    private const string RegularFace = "SaloneraSansRegular";
    private const string BoldFace = "SaloneraSansBold";

    public byte[] GetFont(string faceName)
    {
        var isBold = string.Equals(faceName, BoldFace, StringComparison.Ordinal);
        return File.ReadAllBytes(ResolveFontPath(isBold));
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => new(isBold ? BoldFace : RegularFace);

    private static string ResolveFontPath(bool bold)
    {
        var candidates = OperatingSystem.IsWindows()
            ? new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), bold ? "arialbd.ttf" : "arial.ttf"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), bold ? "segoeuib.ttf" : "segoeui.ttf"),
            }
            : new[]
            {
                bold
                    ? "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
                    : "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                bold
                    ? "/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf"
                    : "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf",
            };
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("PDF üretimi için uygun TrueType yazı tipi bulunamadı.");
    }
}
