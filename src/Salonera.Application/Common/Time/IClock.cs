namespace Salonera.Application.Common.Time;

/// <summary>
/// Uygulamanın zaman kaynağını altyapı ve sistem saatinden ayırır.
/// Kalıcı kayıtlarda UTC, kullanıcı arayüzünde Türkiye saati kullanılmalıdır.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateTimeOffset TurkeyNow { get; }
}
