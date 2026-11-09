using Salonera.Domain.Common;
using Salonera.Domain.Enums;

namespace Salonera.Domain.Entities;

public sealed class PaymentPlan : AuditableEntity
{
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public CurrencyCode Currency { get; set; }
    public PaymentStatus Status { get; set; }
    public ICollection<PaymentReceipt> Collections { get; set; } = [];
}

public sealed class PaymentReceipt : AuditableEntity
{
    public Guid BranchId { get; set; }
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public Guid? PaymentPlanId { get; set; }
    public PaymentPlan? PaymentPlan { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTimeOffset CollectedAt { get; set; }
    public decimal Amount { get; set; }
    public CurrencyCode Currency { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal AmountTry { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public Guid FinancialAccountId { get; set; }
    public bool IsCancelled { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? IdempotencyKey { get; set; }
}

public sealed class Refund : AuditableEntity
{
    public Guid BranchId { get; set; }
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public Guid? CollectionId { get; set; }
    public PaymentReceipt? Collection { get; set; }
    public DateTimeOffset RefundedAt { get; set; }
    public decimal Amount { get; set; }
    public CurrencyCode Currency { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal AmountTry { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid FinancialAccountId { get; set; }
    public bool IsCancelled { get; set; }
}

public sealed class IncomeCategory : AuditableEntity
{
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class ExpenseCategory : AuditableEntity
{
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class Income : AuditableEntity
{
    public Guid BranchId { get; set; }
    public Guid IncomeCategoryId { get; set; }
    public IncomeCategory IncomeCategory { get; set; } = null!;
    public Guid? EventId { get; set; }
    public OrganizationEvent? Event { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? CollectionId { get; set; }
    public PaymentReceipt? Collection { get; set; }
    public DateTimeOffset IncomeDate { get; set; }
    public decimal Amount { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public CurrencyCode Currency { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal AmountTry { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public Guid FinancialAccountId { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Description { get; set; }
    public string? AttachmentPath { get; set; }
    public bool IsCancelled { get; set; }
}

public sealed class Expense : AuditableEntity
{
    public Guid BranchId { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; } = null!;
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid? EventId { get; set; }
    public OrganizationEvent? Event { get; set; }
    public DateTimeOffset ExpenseDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public CurrencyCode Currency { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal AmountTry { get; set; }
    public PaymentStatus Status { get; set; }
    public ExpenseType ExpenseType { get; set; }
    public bool IsRecurring { get; set; }
    public Guid? FinancialAccountId { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Description { get; set; }
    public string? AttachmentPath { get; set; }
    public bool IsCancelled { get; set; }
}

public sealed class Supplier : AuditableEntity
{
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxOffice { get; set; }
    public string? TaxNumber { get; set; }
    public string? Address { get; set; }
    public string? BankInformation { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<SupplierTransaction> Transactions { get; set; } = [];
}

public sealed class SupplierTransaction : AuditableEntity
{
    public Guid BranchId { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public Guid? EventId { get; set; }
    public OrganizationEvent? Event { get; set; }
    public DateTimeOffset TransactionDate { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public CurrencyCode Currency { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public string? DocumentNumber { get; set; }
    public string? Description { get; set; }
    public bool IsCancelled { get; set; }
}

public sealed class CashAccount : AuditableEntity
{
    public Guid BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CurrencyCode Currency { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class BankAccount : AuditableEntity
{
    public Guid BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public string? Iban { get; set; }
    public CurrencyCode Currency { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class FinancialTransaction : AuditableEntity
{
    public Guid BranchId { get; set; }
    public AccountType AccountType { get; set; }
    public Guid AccountId { get; set; }
    public FinancialTransactionType TransactionType { get; set; }
    public DateTimeOffset TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public CurrencyCode Currency { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal AmountTry { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Description { get; set; }
    public bool IsCancelled { get; set; }
    public Guid? ReversalTransactionId { get; set; }
    public Guid? IdempotencyKey { get; set; }
}

public sealed class Transfer : AuditableEntity
{
    public Guid BranchId { get; set; }
    public AccountType FromAccountType { get; set; }
    public Guid FromAccountId { get; set; }
    public AccountType ToAccountType { get; set; }
    public Guid ToAccountId { get; set; }
    public DateTimeOffset TransferDate { get; set; }
    public decimal Amount { get; set; }
    public CurrencyCode Currency { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public string? Description { get; set; }
    public bool IsCancelled { get; set; }
}
