using System.Windows;
using System.Windows.Controls;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class FinanceWindow : Window
{
    private readonly FinanceViewModel _viewModel;

    public FinanceWindow(FinanceViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e) => await _viewModel.LoadAsync();
    private async void RefreshClick(object sender, RoutedEventArgs e) => await _viewModel.LoadAsync();
    private async void AccountChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) await _viewModel.LoadAccountMovementsAsync();
    }
    private async void SupplierChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) await _viewModel.LoadSupplierMovementsAsync();
    }
    private async void CreateAccountClick(object sender, RoutedEventArgs e) => await _viewModel.CreateAccountAsync();
    private async void TransferClick(object sender, RoutedEventArgs e) => await _viewModel.TransferAsync();
    private async void CancelTransferClick(object sender, RoutedEventArgs e) => await _viewModel.CancelTransferAsync();
    private async void CreateIncomeCategoryClick(object sender, RoutedEventArgs e) => await _viewModel.CreateIncomeCategoryAsync();
    private async void CreateExpenseCategoryClick(object sender, RoutedEventArgs e) => await _viewModel.CreateExpenseCategoryAsync();
    private async void CreateIncomeClick(object sender, RoutedEventArgs e) => await _viewModel.CreateIncomeAsync();
    private async void CancelIncomeClick(object sender, RoutedEventArgs e) => await _viewModel.CancelIncomeAsync();
    private async void CreateExpenseClick(object sender, RoutedEventArgs e) => await _viewModel.CreateExpenseAsync();
    private async void PayExpenseClick(object sender, RoutedEventArgs e) => await _viewModel.PayExpenseAsync();
    private async void CancelExpenseClick(object sender, RoutedEventArgs e) => await _viewModel.CancelExpenseAsync();
    private async void CreateSupplierClick(object sender, RoutedEventArgs e) => await _viewModel.CreateSupplierAsync();
}
