using System.Windows;
using System.Windows.Controls;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class CustomerManagementWindow : UserControl
{
    private readonly CustomerManagementViewModel _viewModel;

    public CustomerManagementWindow(CustomerManagementViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e) => await _viewModel.LoadAsync();
    private async void SearchClick(object sender, RoutedEventArgs e) => await _viewModel.LoadAsync();
    private async void CreateClick(object sender, RoutedEventArgs e) => await _viewModel.CreateAsync();
    private async void DeleteClick(object sender, RoutedEventArgs e) => await _viewModel.DeleteSelectedAsync();
}
