using System.Collections.ObjectModel;
using System.Globalization;
using Salonera.Application.Features.Customers;
using Salonera.Application.Features.Events;
using Salonera.Application.Features.Finance;
using Salonera.Application.Common.Security;
using Salonera.Domain.Enums;

namespace Salonera.Desktop.ViewModels;

public sealed record FinanceOption<T>(T Value, string Name) where T : struct, Enum;

public sealed class FinanceAccountForm
{
    public FinanceOption<AccountType>? Type { get; set; }
    public FinanceOption<CurrencyCode>? Currency { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OpeningBalance { get; set; } = "0";
    public string BankName { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
}

public sealed class FinanceTransferForm
{
    public FinancialAccountItem? From { get; set; }
    public FinancialAccountItem? To { get; set; }
    public string Amount { get; set; } = string.Empty;
    public string ExchangeRate { get; set; } = "1";
    public string Description { get; set; } = string.Empty;
}

public sealed class FinanceIncomeForm
{
    public FinanceCategoryItem? Category { get; set; }
    public FinancialAccountItem? Account { get; set; }
    public CalendarEventItem? Event { get; set; }
    public CustomerListItem? Customer { get; set; }
    public string Amount { get; set; } = string.Empty;
    public string ExchangeRate { get; set; } = "1";
    public string VatRate { get; set; } = "0";
    public string Description { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
}

public sealed class FinanceExpenseForm
{
    public FinanceCategoryItem? Category { get; set; }
    public SupplierItem? Supplier { get; set; }
    public CalendarEventItem? Event { get; set; }
    public FinanceOption<CurrencyCode>? Currency { get; set; }
    public string Amount { get; set; } = string.Empty;
    public string ExchangeRate { get; set; } = "1";
    public string VatRate { get; set; } = "0";
    public DateTime? DueDate { get; set; } = DateTime.Today;
    public string Description { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public bool IsRecurring { get; set; }
}

public sealed class FinanceSupplierForm
{
    public string Name { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TaxOffice { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string BankInformation { get; set; } = string.Empty;
}

public sealed class FinanceViewModel(IFinanceService finance, IEventSchedulingService events,
    ICustomerService customers, ICurrentUser currentUser) : ObservableObject
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private FinanceAccountForm _accountForm = new();
    private FinanceTransferForm _transferForm = new();
    private FinanceIncomeForm _incomeForm = new();
    private FinanceExpenseForm _expenseForm = new();
    private FinanceSupplierForm _supplierForm = new();
    private FinancialAccountItem? _selectedAccount;
    private IncomeItem? _selectedIncome;
    private ExpenseItem? _selectedExpense;
    private SupplierItem? _selectedSupplier;
    private TransferItem? _selectedTransfer;
    private string _expensePaymentAmount = string.Empty;
    private FinancialAccountItem? _expensePaymentAccount;
    private string _cancelReason = string.Empty;
    private string _newIncomeCategory = string.Empty;
    private string _newExpenseCategory = string.Empty;
    private string _message = string.Empty;

    public IReadOnlyList<FinanceOption<AccountType>> AccountTypes { get; } =
    [new(AccountType.Cash, "Kasa"), new(AccountType.Bank, "Banka")];
    public IReadOnlyList<FinanceOption<CurrencyCode>> Currencies { get; } =
    [new(CurrencyCode.Try, "TRY"), new(CurrencyCode.Usd, "USD"), new(CurrencyCode.Eur, "EUR")];
    public bool CanManageFinance { get; } =
        currentUser.User?.Permissions.Contains(Permissions.FinanceManage) == true;
    public ObservableCollection<FinancialAccountItem> Accounts { get; } = [];
    public ObservableCollection<FinancialMovementItem> Movements { get; } = [];
    public ObservableCollection<TransferItem> Transfers { get; } = [];
    public ObservableCollection<FinanceCategoryItem> IncomeCategories { get; } = [];
    public ObservableCollection<FinanceCategoryItem> ExpenseCategories { get; } = [];
    public ObservableCollection<IncomeItem> Incomes { get; } = [];
    public ObservableCollection<ExpenseItem> Expenses { get; } = [];
    public ObservableCollection<SupplierItem> Suppliers { get; } = [];
    public ObservableCollection<SupplierMovementItem> SupplierMovements { get; } = [];
    public ObservableCollection<EventCostItem> EventCosts { get; } = [];
    public ObservableCollection<CalendarEventItem> Events { get; } = [];
    public ObservableCollection<CustomerListItem> Customers { get; } = [];

    public FinanceAccountForm AccountForm { get => _accountForm; set => SetProperty(ref _accountForm, value); }
    public FinanceTransferForm TransferForm { get => _transferForm; set => SetProperty(ref _transferForm, value); }
    public FinanceIncomeForm IncomeForm { get => _incomeForm; set => SetProperty(ref _incomeForm, value); }
    public FinanceExpenseForm ExpenseForm { get => _expenseForm; set => SetProperty(ref _expenseForm, value); }
    public FinanceSupplierForm SupplierForm { get => _supplierForm; set => SetProperty(ref _supplierForm, value); }
    public FinancialAccountItem? SelectedAccount { get => _selectedAccount; set => SetProperty(ref _selectedAccount, value); }
    public IncomeItem? SelectedIncome { get => _selectedIncome; set => SetProperty(ref _selectedIncome, value); }
    public ExpenseItem? SelectedExpense { get => _selectedExpense; set => SetProperty(ref _selectedExpense, value); }
    public SupplierItem? SelectedSupplier { get => _selectedSupplier; set => SetProperty(ref _selectedSupplier, value); }
    public TransferItem? SelectedTransfer { get => _selectedTransfer; set => SetProperty(ref _selectedTransfer, value); }
    public FinancialAccountItem? ExpensePaymentAccount { get => _expensePaymentAccount; set => SetProperty(ref _expensePaymentAccount, value); }
    public string ExpensePaymentAmount { get => _expensePaymentAmount; set => SetProperty(ref _expensePaymentAmount, value); }
    public string CancelReason { get => _cancelReason; set => SetProperty(ref _cancelReason, value); }
    public string NewIncomeCategory { get => _newIncomeCategory; set => SetProperty(ref _newIncomeCategory, value); }
    public string NewExpenseCategory { get => _newExpenseCategory; set => SetProperty(ref _newExpenseCategory, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    public async Task LoadAsync()
    {
        try
        {
            var accountId = SelectedAccount?.Id;
            var supplierId = SelectedSupplier?.Id;
            Replace(Accounts, await finance.ListAccountsAsync());
            Replace(Transfers, await finance.ListTransfersAsync());
            Replace(IncomeCategories, await finance.ListIncomeCategoriesAsync());
            Replace(ExpenseCategories, await finance.ListExpenseCategoriesAsync());
            Replace(Incomes, await finance.ListIncomesAsync());
            Replace(Expenses, await finance.ListExpensesAsync());
            Replace(Suppliers, await finance.ListSuppliersAsync());
            Replace(EventCosts, await finance.ListEventCostsAsync());
            var start = DateTimeOffset.Now.AddYears(-2);
            Replace(Events, await events.ListAsync(start, start.AddYears(7)));
            Replace(Customers, await customers.ListAsync());
            SelectedAccount = Accounts.FirstOrDefault(x => x.Id == accountId) ?? Accounts.FirstOrDefault();
            SelectedSupplier = Suppliers.FirstOrDefault(x => x.Id == supplierId) ?? Suppliers.FirstOrDefault();
            if (AccountForm.Type is null || AccountForm.Currency is null)
                AccountForm = new FinanceAccountForm { Type = AccountTypes[0], Currency = Currencies[0] };
            if (ExpenseForm.Currency is null)
                ExpenseForm = new FinanceExpenseForm { Currency = Currencies[0] };
            await LoadAccountMovementsAsync();
            await LoadSupplierMovementsAsync();
            Message = string.Empty;
        }
        catch (Exception exception) { Message = exception.Message; }
    }

    public async Task LoadAccountMovementsAsync()
    {
        try
        {
            Replace(Movements, SelectedAccount is null ? [] :
                await finance.ListMovementsAsync(SelectedAccount.Id, SelectedAccount.Type));
        }
        catch (Exception exception) { Message = exception.Message; }
    }

    public async Task LoadSupplierMovementsAsync()
    {
        try
        {
            Replace(SupplierMovements, SelectedSupplier is null ? [] :
                await finance.ListSupplierMovementsAsync(SelectedSupplier.Id));
        }
        catch (Exception exception) { Message = exception.Message; }
    }

    public async Task CreateAccountAsync()
    {
        if (!TryMoney(AccountForm.OpeningBalance, out var opening) ||
            AccountForm.Type is null || AccountForm.Currency is null)
        { Message = "Hesap türü, para birimi ve açılış bakiyesini kontrol edin."; return; }
        await ActAsync(async () =>
        {
            await finance.CreateAccountAsync(new NewFinancialAccount(AccountForm.Type.Value,
                AccountForm.Code, AccountForm.Name, AccountForm.Currency.Value, opening,
                AccountForm.BankName, AccountForm.Iban));
            AccountForm = new FinanceAccountForm { Type = AccountTypes[0], Currency = Currencies[0] };
        }, "Hesap oluşturuldu.");
    }

    public async Task TransferAsync()
    {
        if (TransferForm.From is null || TransferForm.To is null ||
            !TryMoney(TransferForm.Amount, out var amount) ||
            !TryMoney(TransferForm.ExchangeRate, out var rate))
        { Message = "Virman hesaplarını, tutarı ve kuru kontrol edin."; return; }
        await ActAsync(async () =>
        {
            await finance.TransferAsync(new NewTransfer(Guid.NewGuid(), TransferForm.From.Id,
                TransferForm.From.Type, TransferForm.To.Id, TransferForm.To.Type,
                amount, rate, TransferForm.Description));
            TransferForm = new FinanceTransferForm();
        }, "Virman kaydedildi.");
    }

    public async Task CancelTransferAsync()
    {
        if (SelectedTransfer is null) { Message = "Önce virman seçin."; return; }
        await ActAsync(() => finance.CancelTransferAsync(SelectedTransfer.Id,
            RequiredReason()), "Virman ters kayıtla iptal edildi.");
    }

    public async Task CreateIncomeCategoryAsync()
    {
        await ActAsync(async () =>
        {
            await finance.CreateIncomeCategoryAsync(NewIncomeCategory);
            NewIncomeCategory = string.Empty;
        }, "Gelir kategorisi oluşturuldu.");
    }

    public async Task CreateExpenseCategoryAsync()
    {
        await ActAsync(async () =>
        {
            await finance.CreateExpenseCategoryAsync(NewExpenseCategory);
            NewExpenseCategory = string.Empty;
        }, "Gider kategorisi oluşturuldu.");
    }

    public async Task CreateIncomeAsync()
    {
        if (IncomeForm.Category is null || IncomeForm.Account is null ||
            !TryMoney(IncomeForm.Amount, out var amount) ||
            !TryMoney(IncomeForm.ExchangeRate, out var rate) ||
            !TryMoney(IncomeForm.VatRate, out var vat))
        { Message = "Gelir kategorisi, hesabı ve tutar alanlarını kontrol edin."; return; }
        await ActAsync(async () =>
        {
            await finance.CreateIncomeAsync(new NewIncome(Guid.NewGuid(), IncomeForm.Category.Id,
                IncomeForm.Account.Id, IncomeForm.Account.Type, amount,
                IncomeForm.Account.Currency, rate, vat, IncomeForm.Event?.Id,
                IncomeForm.Customer?.Id, IncomeForm.Description, IncomeForm.DocumentNumber));
            IncomeForm = new FinanceIncomeForm();
        }, "Gelir ve hesap hareketi kaydedildi.");
    }

    public async Task CancelIncomeAsync()
    {
        if (SelectedIncome is null) { Message = "Önce gelir seçin."; return; }
        await ActAsync(() => finance.CancelIncomeAsync(SelectedIncome.Id,
            RequiredReason()), "Gelir ters kayıtla iptal edildi.");
    }

    public async Task CreateExpenseAsync()
    {
        if (ExpenseForm.Category is null || ExpenseForm.Currency is null ||
            !TryMoney(ExpenseForm.Amount, out var amount) ||
            !TryMoney(ExpenseForm.ExchangeRate, out var rate) ||
            !TryMoney(ExpenseForm.VatRate, out var vat))
        { Message = "Gider kategorisi, para birimi ve tutar alanlarını kontrol edin."; return; }
        await ActAsync(async () =>
        {
            await finance.CreateExpenseAsync(new NewExpense(Guid.NewGuid(), ExpenseForm.Category.Id,
                amount, ExpenseForm.Currency.Value, rate, vat, ExpenseForm.Supplier?.Id,
                ExpenseForm.Event?.Id, ExpenseForm.DueDate.HasValue
                    ? DateOnly.FromDateTime(ExpenseForm.DueDate.Value) : null,
                ExpenseForm.Description, ExpenseForm.DocumentNumber,
                ExpenseType.Variable, ExpenseForm.IsRecurring));
            ExpenseForm = new FinanceExpenseForm { Currency = Currencies[0] };
        }, "Gider kaydedildi; varsa tedarikçi carisine işlendi.");
    }

    public async Task PayExpenseAsync()
    {
        if (SelectedExpense is null || ExpensePaymentAccount is null ||
            !TryMoney(ExpensePaymentAmount, out var amount))
        { Message = "Gider, ödeme hesabı ve tutar seçin."; return; }
        await ActAsync(async () =>
        {
            await finance.PayExpenseAsync(new PayExpense(SelectedExpense.Id,
                ExpensePaymentAccount.Id, ExpensePaymentAccount.Type, amount, Guid.NewGuid()));
            ExpensePaymentAmount = string.Empty;
        }, "Gider ödemesi kaydedildi.");
    }

    public async Task CancelExpenseAsync()
    {
        if (SelectedExpense is null) { Message = "Önce gider seçin."; return; }
        await ActAsync(() => finance.CancelExpenseAsync(SelectedExpense.Id,
            RequiredReason()), "Gider ters kayıtla iptal edildi.");
    }

    public async Task CreateSupplierAsync()
    {
        await ActAsync(async () =>
        {
            await finance.CreateSupplierAsync(new NewSupplier(SupplierForm.Name,
                SupplierForm.ContactPerson, SupplierForm.Phone, SupplierForm.Email,
                SupplierForm.TaxOffice, SupplierForm.TaxNumber, SupplierForm.Address,
                SupplierForm.BankInformation));
            SupplierForm = new FinanceSupplierForm();
        }, "Tedarikçi oluşturuldu.");
    }

    private string RequiredReason()
    {
        if (string.IsNullOrWhiteSpace(CancelReason))
            throw new ArgumentException("İptal nedeni girin.");
        return CancelReason;
    }

    private async Task ActAsync(Func<Task> action, string success)
    {
        try
        {
            await action();
            await LoadAsync();
            CancelReason = string.Empty;
            Message = success;
        }
        catch (Exception exception) { Message = exception.Message; }
    }

    private static bool TryMoney(string value, out decimal amount) =>
        decimal.TryParse(value, NumberStyles.Number, Turkish, out amount) ||
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }
}
