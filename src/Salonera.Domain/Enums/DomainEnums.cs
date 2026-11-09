namespace Salonera.Domain.Enums;

public enum EventStatus
{
    Draft = 0,
    Option = 1,
    Contracted = 2,
    Preparing = 3,
    Completed = 4,
    Cancelled = 5,
    Postponed = 6,
}

public enum PaymentMethod
{
    Cash = 0,
    Bank = 1,
    CreditCard = 2,
    Other = 3,
}

public enum PaymentStatus
{
    Pending = 0,
    PartiallyPaid = 1,
    Paid = 2,
    Overdue = 3,
    Cancelled = 4,
}

public enum CurrencyCode
{
    Try = 0,
    Usd = 1,
    Eur = 2,
}

public enum FinancialTransactionType
{
    Income = 0,
    Expense = 1,
    Collection = 2,
    Refund = 3,
    TransferIn = 4,
    TransferOut = 5,
    Reversal = 6,
}

public enum AccountType
{
    Cash = 0,
    Bank = 1,
}

public enum NotificationType
{
    UpcomingEvent = 0,
    OptionExpiring = 1,
    InstallmentDue = 2,
    PaymentOverdue = 3,
    SupplierPaymentDue = 4,
    EventPreparation = 5,
    BackupReminder = 6,
}

public enum ExpenseType
{
    Fixed = 0,
    Variable = 1,
}
