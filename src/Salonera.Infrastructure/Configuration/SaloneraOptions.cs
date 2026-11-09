namespace Salonera.Infrastructure.Configuration;

public sealed class SaloneraOptions
{
    public const string SectionName = "Salonera";

    public string ApplicationName { get; init; } = "Salonera";

    public string Culture { get; init; } = "tr-TR";

    public string BaseCurrency { get; init; } = "TRY";

    public string TimeZoneId { get; init; } = "Europe/Istanbul";
}
