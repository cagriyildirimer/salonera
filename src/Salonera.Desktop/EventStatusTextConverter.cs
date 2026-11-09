using System.Globalization;
using System.Windows.Data;
using Salonera.Domain.Enums;

namespace Salonera.Desktop;

public sealed class EventStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is EventStatus status ? status switch
        {
            EventStatus.Draft => "Taslak",
            EventStatus.Option => "Ön rezervasyon",
            EventStatus.Contracted => "Sözleşmeli",
            EventStatus.Preparing => "Hazırlanıyor",
            EventStatus.Completed => "Tamamlandı",
            EventStatus.Cancelled => "İptal edildi",
            EventStatus.Postponed => "Ertelendi",
            _ => status.ToString(),
        } : value?.ToString() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
