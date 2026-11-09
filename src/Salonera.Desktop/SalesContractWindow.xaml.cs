using System.Diagnostics;
using System.IO;
using System.Windows;
using Salonera.Application.Features.Sales;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class SalesContractWindow : Window
{
    private readonly SalesContractViewModel _viewModel;

    public SalesContractWindow(SalesContractViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e) => await _viewModel.LoadAsync();
    private async void CreatePackageClick(object sender, RoutedEventArgs e) => await _viewModel.CreatePackageAsync();
    private async void CreateExtraClick(object sender, RoutedEventArgs e) => await _viewModel.CreateExtraServiceAsync();

    private async void CreateContractClick(object sender, RoutedEventArgs e)
    {
        var extraIds = ExtraServicesInput.SelectedItems
            .Cast<ExtraServiceListItem>()
            .Select(item => item.Id)
            .ToArray();
        await _viewModel.CreateContractAsync(extraIds);
    }

    private void OpenPdfClick(object sender, RoutedEventArgs e)
    {
        var pdfPath = _viewModel.LastPdfPath;
        if (pdfPath is null || !File.Exists(pdfPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
    }

    private void PrintPdfClick(object sender, RoutedEventArgs e)
    {
        var pdfPath = _viewModel.LastPdfPath;
        if (pdfPath is null || !File.Exists(pdfPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(pdfPath)
        {
            UseShellExecute = true,
            Verb = "print",
        });
    }
}
