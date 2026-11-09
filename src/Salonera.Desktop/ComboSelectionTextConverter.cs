using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using Salonera.Domain.Enums;

namespace Salonera.Desktop;

public sealed class ComboSelectionTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var item = values.ElementAtOrDefault(0);
        if (item is null || item == DependencyProperty.UnsetValue) return string.Empty;

        var path = values.ElementAtOrDefault(1) as string;
        if (!string.IsNullOrWhiteSpace(path))
        {
            var property = item.GetType().GetProperty(path, BindingFlags.Public | BindingFlags.Instance);
            if (property is not null) return Format(property.GetValue(item), culture);
        }

        if (item is EventStatus status)
            return new EventStatusTextConverter().Convert(status, targetType, parameter, culture);

        var number = item.GetType().GetProperty("ContractNumber")?.GetValue(item)?.ToString();
        var customer = item.GetType().GetProperty("CustomerName")?.GetValue(item)?.ToString();
        if (!string.IsNullOrWhiteSpace(number) && !string.IsNullOrWhiteSpace(customer))
            return $"{number} · {customer}";

        return Format(item, culture);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static string Format(object? value, CultureInfo culture)
        => value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("dd.MM.yyyy", culture),
            DateOnly date => date.ToString("dd.MM.yyyy", culture),
            _ => value.ToString() ?? string.Empty,
        };
}
