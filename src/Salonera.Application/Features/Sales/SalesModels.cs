using Salonera.Domain.Enums;

namespace Salonera.Application.Features.Sales;

public sealed record PackageListItem(
    Guid Id,
    string Name,
    int IncludedGuestCount,
    decimal SalesPrice,
    decimal CostPrice,
    bool IsActive);

public sealed record ExtraServiceListItem(
    Guid Id,
    string Name,
    decimal SalesPrice,
    decimal CostPrice,
    bool IsActive);

public sealed record SavePackageRequest(
    string Name,
    int IncludedGuestCount,
    decimal SalesPrice,
    decimal CostPrice,
    decimal? PricePerPerson = null,
    DateOnly? ValidFrom = null,
    DateOnly? ValidUntil = null);

public sealed record SaveExtraServiceRequest(string Name, decimal SalesPrice, decimal CostPrice);

public sealed record PriceLine(string Description, decimal Quantity, decimal UnitPrice, decimal UnitCost);

public sealed record PriceCalculation(
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal VatRate,
    decimal VatAmount,
    decimal NetAmount,
    CurrencyCode Currency,
    decimal ExchangeRate,
    decimal NetAmountTry);

public sealed record CreateContractRequest(
    Guid EventId,
    Guid PackageId,
    IReadOnlyCollection<Guid> ExtraServiceIds,
    decimal DiscountAmount,
    decimal VatRate,
    CurrencyCode Currency,
    decimal ExchangeRate);

public sealed record ContractLineDetail(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalPrice);

public sealed record ContractDocument(
    Guid Id,
    string ContractNumber,
    DateTimeOffset ContractDate,
    string BranchName,
    string CustomerName,
    string CustomerPhone,
    string HallName,
    string EventTypeName,
    DateTimeOffset EventStartsAt,
    DateTimeOffset EventEndsAt,
    int GuestCount,
    IReadOnlyList<ContractLineDetail> Lines,
    PriceCalculation Totals);

public interface IPackageService
{
    Task<IReadOnlyList<PackageListItem>> ListPackagesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExtraServiceListItem>> ListExtraServicesAsync(CancellationToken cancellationToken = default);

    Task<Guid> CreatePackageAsync(SavePackageRequest request, CancellationToken cancellationToken = default);

    Task<Guid> CreateExtraServiceAsync(SaveExtraServiceRequest request, CancellationToken cancellationToken = default);
}

public interface IContractPricingService
{
    PriceCalculation Calculate(
        IEnumerable<PriceLine> lines,
        decimal discountAmount,
        decimal vatRate,
        CurrencyCode currency,
        decimal exchangeRate);
}

public interface ISalesContractService
{
    Task<Guid> CreateContractAsync(CreateContractRequest request, CancellationToken cancellationToken = default);

    Task<ContractDocument> GetContractDocumentAsync(Guid contractId, CancellationToken cancellationToken = default);
}

public interface IContractPdfService
{
    Task<string> GenerateAsync(Guid contractId, string outputPath, CancellationToken cancellationToken = default);

    Task RenderAsync(ContractDocument document, string outputPath, CancellationToken cancellationToken = default);
}
