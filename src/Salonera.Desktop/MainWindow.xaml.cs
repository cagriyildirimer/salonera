using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;

    public MainWindow(MainWindowViewModel viewModel, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _serviceProvider = serviceProvider;

        InitializeComponent();
        DataContext = viewModel;
    }

    private void OpenUsersClick(object sender, RoutedEventArgs e)
    {
        ShowOwnedWindow(_serviceProvider.GetRequiredService<UserManagementWindow>());
    }

    private void OpenCustomersClick(object sender, RoutedEventArgs e)
        => ShowOwnedWindow(_serviceProvider.GetRequiredService<CustomerManagementWindow>());

    private void OpenCatalogsClick(object sender, RoutedEventArgs e)
        => ShowOwnedWindow(_serviceProvider.GetRequiredService<CatalogManagementWindow>());

    private void OpenCalendarClick(object sender, RoutedEventArgs e)
        => ShowOwnedWindow(_serviceProvider.GetRequiredService<CalendarWindow>());

    private void OpenSalesClick(object sender, RoutedEventArgs e)
        => ShowOwnedWindow(_serviceProvider.GetRequiredService<SalesContractWindow>());

    private void OpenPaymentsClick(object sender, RoutedEventArgs e)
        => ShowOwnedWindow(_serviceProvider.GetRequiredService<PaymentWindow>());

    private void ShowOwnedWindow(Window window)
    {
        window.Owner = this;
        window.ShowDialog();
    }
}
