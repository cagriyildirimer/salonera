using Salonera.Domain.Enums;

namespace Salonera.Application.Features.Finance;

public sealed record FinancialAccountItem(Guid Id, AccountType Type, string Code, string Name,
    CurrencyCode Currency, decimal Balance, bool IsActive)
{
    public string TypeText => Type == AccountType.Cash ? "Kasa" : "Banka";
    public string CurrencyText => Currency.ToString().ToUpperInvariant();
    public string DisplayName => $"{Name} ({Currency.ToString().ToUpperInvariant()})";
}

public sealed record FinancialMovementItem(Guid Id, DateTimeOffset Date,
    FinancialTransactionType Type, decimal Amount, CurrencyCode Currency,
    string? Description)
{
    public string TypeText => Type switch
    {
        FinancialTransactionType.Income => "Gelir",
        FinancialTransactionType.Expense => "Gider",
        FinancialTransactionType.Collection => "Tahsilat",
        FinancialTransactionType.Refund => "İade",
        FinancialTransactionType.TransferIn => "Gelen virman",
        FinancialTransactionType.TransferOut => "Giden virman",
        FinancialTransactionType.Reversal => "Ters kayıt",
        _ => Type.ToString(),
    };
}

public sealed record TransferItem(Guid Id, DateTimeOffset Date, string FromAccount,
    string ToAccount, decimal Amount, CurrencyCode Currency, bool IsCancelled,
    string? Description);

public sealed record FinanceCategoryItem(Guid Id, string Name);

public sealed record SupplierItem(Guid Id, string Name, string? Phone, string? TaxNumber,
    decimal BalanceTry, bool IsActive);

public sealed record SupplierMovementItem(Guid Id, DateTimeOffset Date, decimal Debit,
    decimal Credit, CurrencyCode Currency, string? Description);

public sealed record IncomeItem(Guid Id, DateTimeOffset Date, decimal Amount,
    CurrencyCode Currency, string Category, string? Description, bool IsCancelled)
{
    public string CurrencyText => Currency.ToString().ToUpperInvariant();
}

public sealed record ExpenseItem(Guid Id, DateTimeOffset Date, decimal Amount,
    decimal PaidAmount, CurrencyCode Currency, string Category, string? Supplier,
    string? EventNumber, string? Description, bool IsCancelled, PaymentStatus Status)
{
    public string CurrencyText => Currency.ToString().ToUpperInvariant();
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

public sealed record EventCostItem(Guid EventId, string EventNumber, decimal ServiceCostTry,
    decimal DirectExpenseTry, decimal TotalCostTry, int ExpenseCount);

public sealed record NewFinancialAccount(AccountType Type, string Code, string Name,
    CurrencyCode Currency, decimal OpeningBalance, string? BankName = null, string? Iban = null);

public sealed record NewSupplier(string Name, string? ContactPerson = null, string? Phone = null,
    string? Email = null, string? TaxOffice = null, string? TaxNumber = null,
    string? Address = null, string? BankInformation = null);

public sealed record NewIncome(Guid OperationId, Guid CategoryId, Guid AccountId,
    AccountType AccountType, decimal Amount, CurrencyCode Currency, decimal ExchangeRate,
    decimal VatRate, Guid? EventId = null, Guid? CustomerId = null,
    string? Description = null, string? DocumentNumber = null);

public sealed record NewExpense(Guid OperationId, Guid CategoryId, decimal Amount,
    CurrencyCode Currency, decimal ExchangeRate, decimal VatRate,
    Guid? SupplierId = null, Guid? EventId = null, DateOnly? DueDate = null,
    string? Description = null, string? DocumentNumber = null,
    ExpenseType ExpenseType = ExpenseType.Variable, bool IsRecurring = false);

public sealed record PayExpense(Guid ExpenseId, Guid AccountId, AccountType AccountType,
    decimal Amount, Guid OperationId);

public sealed record NewTransfer(Guid OperationId, Guid FromAccountId,
    AccountType FromAccountType, Guid ToAccountId, AccountType ToAccountType,
    decimal Amount, decimal ExchangeRate, string? Description = null);

public interface IFinanceService
{
    Task<IReadOnlyList<FinancialAccountItem>> ListAccountsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<FinancialMovementItem>> ListMovementsAsync(Guid accountId,
        AccountType type, CancellationToken ct = default);
    Task<Guid> CreateAccountAsync(NewFinancialAccount request, CancellationToken ct = default);
    Task<Guid> TransferAsync(NewTransfer request, CancellationToken ct = default);
    Task<IReadOnlyList<TransferItem>> ListTransfersAsync(CancellationToken ct = default);
    Task CancelTransferAsync(Guid transferId, string reason, CancellationToken ct = default);

    Task<IReadOnlyList<FinanceCategoryItem>> ListIncomeCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<FinanceCategoryItem>> ListExpenseCategoriesAsync(CancellationToken ct = default);
    Task<Guid> CreateIncomeCategoryAsync(string name, CancellationToken ct = default);
    Task<Guid> CreateExpenseCategoryAsync(string name, CancellationToken ct = default);

    Task<IReadOnlyList<IncomeItem>> ListIncomesAsync(CancellationToken ct = default);
    Task<Guid> CreateIncomeAsync(NewIncome request, CancellationToken ct = default);
    Task CancelIncomeAsync(Guid incomeId, string reason, CancellationToken ct = default);

    Task<IReadOnlyList<ExpenseItem>> ListExpensesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EventCostItem>> ListEventCostsAsync(CancellationToken ct = default);
    Task<Guid> CreateExpenseAsync(NewExpense request, CancellationToken ct = default);
    Task PayExpenseAsync(PayExpense request, CancellationToken ct = default);
    Task CancelExpenseAsync(Guid expenseId, string reason, CancellationToken ct = default);

    Task<IReadOnlyList<SupplierItem>> ListSuppliersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SupplierMovementItem>> ListSupplierMovementsAsync(Guid supplierId,
        CancellationToken ct = default);
    Task<Guid> CreateSupplierAsync(NewSupplier request, CancellationToken ct = default);
}
