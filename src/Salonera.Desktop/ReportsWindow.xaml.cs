using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class ReportsWindow : UserControl
{
    private readonly ReportsViewModel _viewModel;

    public ReportsWindow(ReportsViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e) => await _viewModel.LoadAsync();
    private async void RefreshClick(object sender, RoutedEventArgs e) => await _viewModel.RefreshAsync();

    private async void ClearFiltersClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Hall = null;
        _viewModel.Customer = null;
        _viewModel.Supplier = null;
        await _viewModel.RefreshAsync();
    }

    private async void ExportClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string extension }) return;
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Salonera", "Raporlar");
        Directory.CreateDirectory(directory);
        var dialog = new SaveFileDialog
        {
            InitialDirectory = directory,
            FileName = $"salonera-rapor-{DateTime.Now:yyyyMMdd-HHmm}{extension}",
            DefaultExt = extension,
            Filter = extension switch
            {
                ".xlsx" => "Excel çalışma kitabı (*.xlsx)|*.xlsx",
                ".pdf" => "PDF dosyası (*.pdf)|*.pdf",
                _ => "CSV dosyası (*.csv)|*.csv",
            },
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) await _viewModel.ExportAsync(dialog.FileName);
    }
}
