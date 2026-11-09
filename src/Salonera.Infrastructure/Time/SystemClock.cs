using Salonera.Application.Common.Time;

namespace Salonera.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    private static readonly TimeZoneInfo TurkeyTimeZone = ResolveTurkeyTimeZone();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateTimeOffset TurkeyNow => TimeZoneInfo.ConvertTime(UtcNow, TurkeyTimeZone);

    private static TimeZoneInfo ResolveTurkeyTimeZone()
    {
        string[] timeZoneIds = OperatingSystem.IsWindows()
            ? ["Turkey Standard Time", "Europe/Istanbul"]
            : ["Europe/Istanbul", "Turkey Standard Time"];

        foreach (var timeZoneId in timeZoneIds)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                // Bir sonraki platform kimliği denenir.
            }
            catch (InvalidTimeZoneException)
            {
                // Bir sonraki platform kimliği denenir.
            }
        }

        throw new InvalidOperationException("Türkiye saat dilimi sistemde bulunamadı.");
    }
}
