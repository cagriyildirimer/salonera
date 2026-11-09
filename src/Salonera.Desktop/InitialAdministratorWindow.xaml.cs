using System.Windows;
using System.Windows.Controls;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class InitialAdministratorWindow : UserControl
{
    private readonly InitialAdministratorViewModel _viewModel;
    public event EventHandler? Completed;

    public InitialAdministratorWindow(InitialAdministratorViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void CreateClick(object sender, RoutedEventArgs e)
    {
        if (await _viewModel.CreateAsync(PasswordInput.Password, ConfirmationInput.Password))
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}
