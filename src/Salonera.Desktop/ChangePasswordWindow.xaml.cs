using System.Windows;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class ChangePasswordWindow : Window
{
    private readonly ChangePasswordViewModel _viewModel;

    public ChangePasswordWindow(ChangePasswordViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void ChangeClick(object sender, RoutedEventArgs e)
    {
        if (await _viewModel.ChangeAsync(
                CurrentPasswordInput.Password,
                NewPasswordInput.Password,
                ConfirmationInput.Password))
        {
            DialogResult = true;
        }
    }
}
