using System.Data;
using Salonera.Application.Features.Catalogs;
using Salonera.Application.Features.Customers;
using Salonera.Application.Features.Finance;
using Salonera.Application.Features.Reports;

namespace Salonera.Desktop.ViewModels;

public sealed record ReportKindOption(ReportKind Value, string Name);

public sealed class ReportsViewModel(IReportService reports, IReportExportService exports,
    ICatalogService catalogs, ICustomerService customers, IFinanceService finance) : ObservableObject
{
    private DateTime? _from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime? _to = DateTime.Today;
    private ReportKindOption? _kind;
    private HallListItem? _hall;
    private CustomerListItem? _customer;
    private SupplierItem? _supplier;
    private DataView? _tableView;
    private string _title = "Rapor seçin";
    private string _note = string.Empty;
    private string _message = string.Empty;
    private ReportTable? _current;

    public IReadOnlyList<ReportKindOption> Kinds { get; } =
    [
        new(ReportKind.DailyCash, "Günlük Kasa"),
        new(ReportKind.IncomeExpense, "Gelir, Gider ve Kâr/Zarar"),
        new(ReportKind.EventProfit, "Organizasyon Kârlılığı"),
        new(ReportKind.CustomerBalances, "Müşteri Bakiyeleri"),
        new(ReportKind.SupplierBalances, "Tedarikçi Bakiyeleri"),
    ];
    public IReadOnlyList<HallListItem> Halls { get; private set; } = [];
    public IReadOnlyList<CustomerListItem> Customers { get; private set; } = [];
    public IReadOnlyList<SupplierItem> Suppliers { get; private set; } = [];
    public DateTime? From { get => _from; set => SetProperty(ref _from, value); }
    public DateTime? To { get => _to; set => SetProperty(ref _to, value); }
    public ReportKindOption? Kind
    {
        get => _kind;
        set
        {
            if (!SetProperty(ref _kind, value)) return;
            Hall = null;
            Customer = null;
            Supplier = null;
            OnPropertyChanged(nameof(CanFilterHall));
            OnPropertyChanged(nameof(CanFilterCustomer));
            OnPropertyChanged(nameof(CanFilterSupplier));
        }
    }
    public bool CanFilterHall => Kind?.Value is ReportKind.IncomeExpense or ReportKind.EventProfit;
    public bool CanFilterCustomer => Kind?.Value is ReportKind.IncomeExpense or
        ReportKind.EventProfit or ReportKind.CustomerBalances;
    public bool CanFilterSupplier => Kind?.Value == ReportKind.SupplierBalances;
    public HallListItem? Hall { get => _hall; set => SetProperty(ref _hall, value); }
    public CustomerListItem? Customer { get => _customer; set => SetProperty(ref _customer, value); }
    public SupplierItem? Supplier { get => _supplier; set => SetProperty(ref _supplier, value); }
    public DataView? TableView { get => _tableView; private set => SetProperty(ref _tableView, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string Note { get => _note; private set => SetProperty(ref _note, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    public async Task LoadAsync()
    {
        try
        {
            Halls = await catalogs.ListHallsAsync();
            Customers = await customers.ListAsync();
            Suppliers = await finance.ListSuppliersAsync();
            OnPropertyChanged(nameof(Halls));
            OnPropertyChanged(nameof(Customers));
            OnPropertyChanged(nameof(Suppliers));
            Kind ??= Kinds[0];
            await RefreshAsync();
        }
        catch (Exception exception) { Message = exception.Message; }
    }

    public async Task RefreshAsync()
    {
        if (From is null || To is null || Kind is null)
        {
            Message = "Rapor türü ve tarih aralığı seçin.";
            return;
        }
        try
        {
            _current = await reports.GetAsync(Kind.Value, new ReportFilter(
                DateOnly.FromDateTime(From.Value), DateOnly.FromDateTime(To.Value),
                Hall?.Id, Customer?.Id, Supplier?.Id));
            var table = new DataTable();
            foreach (var column in _current.Columns) table.Columns.Add(column, typeof(string));
            foreach (var row in _current.Rows) table.Rows.Add(row.Cells.Cast<object>().ToArray());
            TableView = table.DefaultView;
            Title = _current.Title;
            Note = _current.Note;
            Message = $"{_current.Rows.Count} satır gösteriliyor.";
        }
        catch (Exception exception) { Message = exception.Message; }
    }

    public async Task<string?> ExportAsync(string path)
    {
        if (_current is null) { Message = "Önce raporu oluşturun."; return null; }
        try
        {
            var result = await exports.ExportAsync(_current, path);
            Message = $"Rapor kaydedildi: {result}";
            return result;
        }
        catch (Exception exception) { Message = exception.Message; return null; }
    }

}
