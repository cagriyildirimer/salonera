using System.Windows;
using Salonera.Application.Features.Events;

namespace Salonera.Desktop;

public partial class EventDetailWindow : Window
{
    public EventDetailWindow(CalendarEventItem organizationEvent)
    {
        ArgumentNullException.ThrowIfNull(organizationEvent);
        InitializeComponent();
        DataContext = organizationEvent;
    }
}
