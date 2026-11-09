using System.Windows;
using System.Windows.Controls;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class ChangePasswordWindow : UserControl
{
    private readonly ChangePasswordViewModel _viewModel;
    public event EventHandler? Completed;

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
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}
