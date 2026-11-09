using System.Windows;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class CalendarWindow : Window
{
    private readonly CalendarViewModel _viewModel;

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

        var detailWindow = new EventDetailWindow(_viewModel.SelectedEvent) { Owner = this };
        detailWindow.ShowDialog();
    }
}
