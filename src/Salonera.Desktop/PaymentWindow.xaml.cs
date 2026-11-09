using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class PaymentWindow : Window
{
    private readonly PaymentViewModel _viewModel;

    public PaymentWindow(PaymentViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e) => await _viewModel.LoadAsync();
    private async void ContractChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) await _viewModel.RefreshAsync();
    }
    private async void RefreshClick(object sender, RoutedEventArgs e) => await _viewModel.LoadAsync();
    private void AddPlanClick(object sender, RoutedEventArgs e) => _viewModel.AddPlanItem();
    private void ClearPlanClick(object sender, RoutedEventArgs e) => _viewModel.ClearPlan();
    private async void SavePlanClick(object sender, RoutedEventArgs e) => await _viewModel.SavePlanAsync();
    private async void CollectClick(object sender, RoutedEventArgs e) => await _viewModel.CollectAsync();
    private async void RefundClick(object sender, RoutedEventArgs e) => await _viewModel.RefundAsync();
    private async void CancelClick(object sender, RoutedEventArgs e)
    {
        var receipt = _viewModel.SelectedReceipt;
        if (receipt is null || receipt.IsCancelled || string.IsNullOrWhiteSpace(_viewModel.Reason))
        {
            await _viewModel.CancelAsync();
            return;
        }

        var refundNote = receipt.Refunded > 0
            ? $"Bu tahsilattan {receipt.Refunded:N2} tutarında iade yapılmış. İade kaydı saklanacak; kalan net tutar ters kayıtla kapatılacak.\n\n"
            : string.Empty;
        var answer = MessageBox.Show(
            $"{refundNote}{receipt.ReceiptNumber} numaralı tahsilatı iptal etmek istediğinize emin misiniz?",
            "Tahsilatı İptal Et",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer == MessageBoxResult.Yes) await _viewModel.CancelAsync();
    }
    private async void OpenReceiptClick(object sender, RoutedEventArgs e)
    {
        var path = await _viewModel.CreatePdfAsync();
        if (path is not null) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private async void PrintReceiptClick(object sender, RoutedEventArgs e)
    {
        var path = await _viewModel.CreatePdfAsync();
        if (path is not null)
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "print" });
    }
}
