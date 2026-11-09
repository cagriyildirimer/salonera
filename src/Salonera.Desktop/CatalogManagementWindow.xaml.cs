using System.Windows;
using System.Windows.Controls;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class CatalogManagementWindow : UserControl
{
    private readonly CatalogManagementViewModel _viewModel;

    public CatalogManagementWindow(CatalogManagementViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e) => await _viewModel.LoadAsync();
    private async void CreateBranchClick(object sender, RoutedEventArgs e) => await _viewModel.CreateBranchAsync();
    private async void CreateHallClick(object sender, RoutedEventArgs e) => await _viewModel.CreateHallAsync();
    private async void CreateEventTypeClick(object sender, RoutedEventArgs e) => await _viewModel.CreateEventTypeAsync();
}
