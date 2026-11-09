using Salonera.Domain.Enums;

namespace Salonera.Application.Features.Payments;

public sealed record ContractBalance(
    Guid ContractId, string ContractNumber, string CustomerName, decimal Total,
    decimal Collected, decimal Refunded, decimal Remaining, CurrencyCode Currency,
    IReadOnlyList<InstallmentItem> Installments, IReadOnlyList<ReceiptItem> Receipts);

public sealed record InstallmentItem(Guid Id, DateOnly DueDate, decimal Amount, decimal Paid,
    PaymentStatus Status)
{
    public string StatusText => Status switch
    {
        PaymentStatus.Pending => "Bekliyor",
        PaymentStatus.PartiallyPaid => "Kısmi ödeme",
        PaymentStatus.Paid => "Ödendi",
        PaymentStatus.Overdue => "Gecikti",
        PaymentStatus.Cancelled => "İptal",
        _ => Status.ToString(),
    };
}

public sealed record ReceiptItem(Guid Id, string ReceiptNumber, DateTimeOffset CollectedAt,
    decimal Amount, decimal Refunded, CurrencyCode Currency, PaymentMethod Method,
    bool IsCancelled, Guid? PaymentPlanId)
{
    public string MethodText => Method switch
    {
        PaymentMethod.Cash => "Nakit",
        PaymentMethod.Bank => "Banka",
        PaymentMethod.CreditCard => "Kredi kartı",
        PaymentMethod.Other => "Diğer",
        _ => Method.ToString(),
    };
}

public sealed record PaymentMethodOption(PaymentMethod Value, string Name);

public sealed record ReceiptDocument(ContractBalance Balance, ReceiptItem Receipt);

public sealed record ContractPaymentListItem(Guid Id, string ContractNumber, string CustomerName,
    decimal Total, CurrencyCode Currency);

public sealed record NewInstallment(DateOnly DueDate, decimal Amount);

public sealed record CollectPaymentRequest(Guid ContractId, Guid? PaymentPlanId, decimal Amount,
    decimal ExchangeRate, PaymentMethod Method, Guid IdempotencyKey);

public sealed record RefundPaymentRequest(Guid ReceiptId, decimal Amount, string Reason);

public interface IPaymentService
{
    Task<IReadOnlyList<ContractPaymentListItem>> ListContractsAsync(CancellationToken cancellationToken = default);
    Task<ContractBalance> GetBalanceAsync(Guid contractId, CancellationToken cancellationToken = default);
    Task<ReceiptDocument> GetReceiptAsync(Guid receiptId, CancellationToken cancellationToken = default);
    Task SetPlanAsync(Guid contractId, IReadOnlyList<NewInstallment> installments,
        CancellationToken cancellationToken = default);
    Task<Guid> CollectAsync(CollectPaymentRequest request, CancellationToken cancellationToken = default);
    Task CancelReceiptAsync(Guid receiptId, string reason, CancellationToken cancellationToken = default);
    Task<Guid> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default);
}

public interface IReceiptPdfService
{
    Task<string> GenerateAsync(Guid receiptId, string outputPath,
        CancellationToken cancellationToken = default);
}
