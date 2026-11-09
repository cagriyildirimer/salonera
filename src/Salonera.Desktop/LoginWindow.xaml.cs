using System.Windows;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void LoginClick(object sender, RoutedEventArgs e)
    {
        if (await _viewModel.LoginAsync(PasswordInput.Password))
        {
            DialogResult = true;
        }
    }
}
