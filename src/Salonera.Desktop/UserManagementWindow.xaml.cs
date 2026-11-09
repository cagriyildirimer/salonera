using System.Windows;
using System.Windows.Controls;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class UserManagementWindow : UserControl
{
    private readonly UserManagementViewModel _viewModel;

    public UserManagementWindow(UserManagementViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e) => await _viewModel.LoadAsync();

    private async void CreateClick(object sender, RoutedEventArgs e)
    {
        if (await _viewModel.CreateAsync(PasswordInput.Password))
        {
            PasswordInput.Clear();
        }
    }

    private async void ToggleActiveClick(object sender, RoutedEventArgs e)
        => await _viewModel.ToggleSelectedUserAsync();
}
