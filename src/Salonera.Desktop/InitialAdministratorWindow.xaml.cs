using System.Windows;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class InitialAdministratorWindow : Window
{
    private readonly InitialAdministratorViewModel _viewModel;

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
            DialogResult = true;
        }
    }
}
