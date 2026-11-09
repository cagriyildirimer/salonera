using System.Windows;
using System.Windows.Controls;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class LoginWindow : UserControl
{
    private readonly LoginViewModel _viewModel;
    public event EventHandler? Completed;

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
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}
