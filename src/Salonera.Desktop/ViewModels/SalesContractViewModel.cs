using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;
using Salonera.Application.Common.Time;
using Salonera.Application.Features.Events;
using Salonera.Application.Features.Sales;
using Salonera.Domain.Enums;

namespace Salonera.Desktop.ViewModels;

public sealed class SalesContractViewModel(
    IPackageService packageService,
    IEventSchedulingService eventSchedulingService,
    ISalesContractService contractService,
    IContractPdfService pdfService,
    IClock clock,
    ILogger<SalesContractViewModel> logger) : ObservableObject
{
    private PackageListItem? _selectedPackage;
    private CalendarEventItem? _selectedEvent;
    private string _discount = "0";
    private string _vatRate = "20";
    private string _exchangeRate = "1";
    private CurrencyCode _currency = CurrencyCode.Try;
    private string _packageName = string.Empty;
    private string _packageSalesPrice = string.Empty;
    private string _packageCostPrice = string.Empty;
    private string _includedGuests = "100";
    private string _extraName = string.Empty;
    private string _extraSalesPrice = string.Empty;
    private string _extraCostPrice = string.Empty;
    private string _message = string.Empty;
    private string? _lastPdfPath;

    public ObservableCollection<PackageListItem> Packages { get; } = [];
    public ObservableCollection<ExtraServiceListItem> ExtraServices { get; } = [];
    public ObservableCollection<CalendarEventItem> Events { get; } = [];
    public IReadOnlyList<CurrencyCode> Currencies { get; } = Enum.GetValues<CurrencyCode>();
    public PackageListItem? SelectedPackage { get => _selectedPackage; set => SetProperty(ref _selectedPackage, value); }
    public CalendarEventItem? SelectedEvent { get => _selectedEvent; set => SetProperty(ref _selectedEvent, value); }
    public string Discount { get => _discount; set => SetProperty(ref _discount, value); }
    public string VatRate { get => _vatRate; set => SetProperty(ref _vatRate, value); }
    public string ExchangeRate { get => _exchangeRate; set => SetProperty(ref _exchangeRate, value); }
    public CurrencyCode Currency { get => _currency; set => SetProperty(ref _currency, value); }
    public string PackageName { get => _packageName; set => SetProperty(ref _packageName, value); }
    public string PackageSalesPrice { get => _packageSalesPrice; set => SetProperty(ref _packageSalesPrice, value); }
    public string PackageCostPrice { get => _packageCostPrice; set => SetProperty(ref _packageCostPrice, value); }
    public string IncludedGuests { get => _includedGuests; set => SetProperty(ref _includedGuests, value); }
    public string ExtraName { get => _extraName; set => SetProperty(ref _extraName, value); }
    public string ExtraSalesPrice { get => _extraSalesPrice; set => SetProperty(ref _extraSalesPrice, value); }
    public string ExtraCostPrice { get => _extraCostPrice; set => SetProperty(ref _extraCostPrice, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string? LastPdfPath { get => _lastPdfPath; private set => SetProperty(ref _lastPdfPath, value); }

    public async Task LoadAsync()
    {
        Replace(Packages, await packageService.ListPackagesAsync());
        Replace(ExtraServices, await packageService.ListExtraServicesAsync());
        var start = clock.TurkeyNow.AddYears(-1);
        Replace(Events, (await eventSchedulingService.ListAsync(start, start.AddYears(3)))
            .Where(item => item.Status != EventStatus.Cancelled));
        SelectedPackage ??= Packages.FirstOrDefault();
        SelectedEvent ??= Events.FirstOrDefault();
    }

    public async Task CreatePackageAsync()
    {
        if (!TryMoney(PackageSalesPrice, out var sales) || !TryMoney(PackageCostPrice, out var cost) ||
            !int.TryParse(IncludedGuests, NumberStyles.Integer, CultureInfo.InvariantCulture, out var guests))
        {
            Message = "Paket fiyatlarını ve kişi sayısını kontrol edin.";
            return;
        }

        await packageService.CreatePackageAsync(new SavePackageRequest(PackageName, guests, sales, cost));
        Message = "Paket oluşturuldu.";
        await LoadAsync();
    }

    public async Task CreateExtraServiceAsync()
    {
        if (!TryMoney(ExtraSalesPrice, out var sales) || !TryMoney(ExtraCostPrice, out var cost))
        {
            Message = "Ek hizmet fiyatlarını kontrol edin.";
            return;
        }

        await packageService.CreateExtraServiceAsync(new SaveExtraServiceRequest(ExtraName, sales, cost));
        Message = "Ek hizmet oluşturuldu.";
        await LoadAsync();
    }

    public async Task<bool> CreateContractAsync(IReadOnlyCollection<Guid> extraServiceIds)
    {
        if (SelectedEvent is null || SelectedPackage is null ||
            !TryMoney(Discount, out var discount) ||
            !TryMoney(VatRate, out var vatRate) ||
            !TryMoney(ExchangeRate, out var exchangeRate))
        {
            Message = "Organizasyon, paket ve mali alanları kontrol edin.";
            return false;
        }

        Guid? contractId = null;
        try
        {
            contractId = await contractService.FindContractIdForEventAsync(SelectedEvent.Id);
            var existingContract = contractId.HasValue;
            if (!existingContract)
            {
                contractId = await contractService.CreateContractAsync(new CreateContractRequest(
                    SelectedEvent.Id,
                    SelectedPackage.Id,
                    extraServiceIds,
                    discount,
                    vatRate,
                    Currency,
                    exchangeRate));
            }

            var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var outputPath = Path.Combine(localData, "Salonera", "Sozlesmeler", $"sozlesme-{contractId.Value:N}.pdf");
            LastPdfPath = await pdfService.GenerateAsync(contractId.Value, outputPath);
            Message = existingContract
                ? "Mevcut sözleşmenin PDF'si oluşturuldu."
                : "Sözleşme ve PDF başarıyla oluşturuldu.";
            await LoadAsync();
            return true;
        }
        catch (InvalidOperationException exception)
        {
            Message = contractId.HasValue
                ? $"Sözleşme kayıtlı, ancak PDF oluşturulamadı: {exception.Message}"
                : exception.Message;
            return false;
        }
        catch (ArgumentException exception)
        {
            Message = exception.Message;
            return false;
        }
        catch (Exception exception)
        {
            logger.Log(
                LogLevel.Error,
                new EventId(3002, "ContractPdfFailed"),
                "Sözleşme veya PDF oluşturulamadı.",
                exception,
                static (state, _) => state);
            Message = contractId.HasValue
                ? "Sözleşme kayıtlı, ancak PDF oluşturulamadı. Ayrıntılar uygulama günlüğüne kaydedildi."
                : "Sözleşme oluşturulamadı. Ayrıntılar uygulama günlüğüne kaydedildi.";
            return false;
        }
    }

    private static bool TryMoney(string value, out decimal result)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out result) ||
           decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }
}
