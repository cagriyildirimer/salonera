using System.Windows;
using System.Windows.Controls;
using Salonera.Application.Features.Events;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class CalendarWindow : UserControl
{
    private readonly CalendarViewModel _viewModel;
    public event Action<CalendarEventItem>? DetailRequested;

    public CalendarWindow(CalendarViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e) => await _viewModel.LoadAsync();
    private async void RefreshClick(object sender, RoutedEventArgs e) => await _viewModel.LoadCalendarAsync();
    private async void ScheduleClick(object sender, RoutedEventArgs e) => await _viewModel.ScheduleAsync();
    private async void CancelClick(object sender, RoutedEventArgs e) => await _viewModel.CancelSelectedAsync();

    private void EventDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedEvent is null)
        {
            return;
        }

        DetailRequested?.Invoke(_viewModel.SelectedEvent);
    }
}
