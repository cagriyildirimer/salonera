using System.Windows;
using System.Windows.Controls;
using Salonera.Application.Features.Events;

namespace Salonera.Desktop;

public partial class EventDetailWindow : UserControl
{
    public event EventHandler? BackRequested;

    public EventDetailWindow(CalendarEventItem organizationEvent)
    {
        ArgumentNullException.ThrowIfNull(organizationEvent);
        InitializeComponent();
        DataContext = organizationEvent;
    }

    private void BackClick(object sender, RoutedEventArgs e)
        => BackRequested?.Invoke(this, EventArgs.Empty);
}
