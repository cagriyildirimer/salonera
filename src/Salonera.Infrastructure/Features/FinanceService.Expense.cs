using Microsoft.EntityFrameworkCore;
using Salonera.Application.Common.Security;
using Salonera.Application.Features.Finance;
using Salonera.Domain.Entities;
using Salonera.Domain.Enums;

namespace Salonera.Infrastructure.Features;

public sealed partial class FinanceService
{
    public async Task<IReadOnlyList<FinanceCategoryItem>> ListExpenseCategoriesAsync(CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceView);
        var (scope, db) = Context();
        await using var disposal = scope;
        return await db.ExpenseCategories.AsNoTracking()
            .Where(x => x.BranchId == user.BranchId && x.IsActive)
            .OrderBy(x => x.Name).Select(x => new FinanceCategoryItem(x.Id, x.Name))
            .ToListAsync(ct);
    }

    public async Task<Guid> CreateExpenseCategoryAsync(string name, CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceManage);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        name = name.Trim();
        if (name.Length > 100) throw new ArgumentException("Kategori adı çok uzun.");
        var (scope, db) = Context();
        await using var disposal = scope;
        if (await db.ExpenseCategories.AnyAsync(x => x.BranchId == user.BranchId && x.Name == name, ct))
            throw new InvalidOperationException("Bu gider kategorisi zaten var.");
        var category = new ExpenseCategory { BranchId = user.BranchId, Name = name };
        db.ExpenseCategories.Add(category);
        db.AuditLogs.Add(Audit(user, nameof(ExpenseCategory), category.Id, "EXPENSE_CATEGORY_CREATED"));
        await db.SaveChangesAsync(ct);
        return category.Id;
    }

    public async Task<IReadOnlyList<ExpenseItem>> ListExpensesAsync(CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceView);
        var (scope, db) = Context();
        await using var disposal = scope;
        var expenses = await db.Expenses.AsNoTracking()
            .Include(x => x.ExpenseCategory).Include(x => x.Supplier).Include(x => x.Event)
            .Where(x => x.BranchId == user.BranchId)
            .OrderByDescending(x => x.ExpenseDate).ToListAsync(ct);
        var today = DateOnly.FromDateTime(_clock.TurkeyNow.Date);
        return expenses.Select(x => new ExpenseItem(x.Id, x.ExpenseDate, x.Amount,
            x.PaidAmount, x.Currency, x.ExpenseCategory.Name, x.Supplier?.Name,
            x.Event?.EventNumber, x.Description, x.IsCancelled,
            x.IsCancelled ? PaymentStatus.Cancelled
                : x.PaidAmount >= x.Amount ? PaymentStatus.Paid
                : x.DueDate < today ? PaymentStatus.Overdue
                : x.PaidAmount > 0 ? PaymentStatus.PartiallyPaid : PaymentStatus.Pending)).ToArray();
    }

    public async Task<IReadOnlyList<EventCostItem>> ListEventCostsAsync(CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceView);
        var (scope, db) = Context();
        await using var disposal = scope;
        var expenses = await db.Expenses.AsNoTracking()
            .Where(x => x.BranchId == user.BranchId && x.EventId.HasValue && !x.IsCancelled)
            .ToListAsync(ct);
        var serviceLines = await db.EventServices.AsNoTracking()
            .Include(x => x.Event).ThenInclude(x => x.Contract)
            .Where(x => x.Event.BranchId == user.BranchId).ToListAsync(ct);
        var direct = expenses.GroupBy(x => x.EventId!.Value)
            .ToDictionary(x => x.Key, x => (Amount: x.Sum(y => y.AmountTry), Count: x.Count()));
        var service = serviceLines.GroupBy(x => x.EventId)
            .ToDictionary(x => x.Key, x => x.Sum(y => decimal.Round(
                y.Quantity * y.UnitCostSnapshot * (y.Event.Contract?.ExchangeRate ?? 1m), 2)));
        var ids = direct.Keys.Concat(service.Keys).Distinct().ToArray();
        var events = await db.Events.AsNoTracking().Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.EventNumber }).ToListAsync(ct);
        return events.Select(x =>
        {
            var directCost = direct.GetValueOrDefault(x.Id);
            var serviceCost = service.GetValueOrDefault(x.Id);
            return new EventCostItem(x.Id, x.EventNumber, serviceCost, directCost.Amount,
                serviceCost + directCost.Amount, directCost.Count);
        }).OrderByDescending(x => x.TotalCostTry).ToArray();
    }

    public async Task<Guid> CreateExpenseAsync(NewExpense request, CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceManage);
        ArgumentNullException.ThrowIfNull(request);
        ValidateMoney(request.Amount);
        ValidateRate(request.Currency, request.ExchangeRate);
        ValidateVat(request.VatRate);
        if (request.OperationId == Guid.Empty)
            throw new ArgumentException("İşlem anahtarı boş olamaz.");
        var (scope, db) = Context();
        await using var disposal = scope;
        await using var tx = await BeginAsync(db, ct);
        await LockAsync(db, OperationLock(request.OperationId), ct);
        var existing = await db.Expenses.SingleOrDefaultAsync(x => x.Id == request.OperationId, ct);
        if (existing is not null)
        {
            if (existing.BranchId != user.BranchId || existing.Amount != request.Amount ||
                existing.ExpenseCategoryId != request.CategoryId || existing.SupplierId != request.SupplierId ||
                existing.EventId != request.EventId || existing.Currency != request.Currency)
                throw new InvalidOperationException("İşlem anahtarı farklı bir giderde kullanılmış.");
            return existing.Id;
        }
        if (!await db.ExpenseCategories.AnyAsync(x => x.Id == request.CategoryId &&
            x.BranchId == user.BranchId && x.IsActive, ct))
            throw new InvalidOperationException("Gider kategorisi bulunamadı.");
        if (request.SupplierId.HasValue && !await db.Suppliers.AnyAsync(x =>
            x.Id == request.SupplierId && x.BranchId == user.BranchId && x.IsActive, ct))
            throw new InvalidOperationException("Tedarikçi bu şubeye ait değil.");
        if (request.EventId.HasValue && !await db.Events.AnyAsync(x =>
            x.Id == request.EventId && x.BranchId == user.BranchId, ct))
            throw new InvalidOperationException("Organizasyon bu şubeye ait değil.");
        var expense = new Expense
        {
            Id = request.OperationId,
            BranchId = user.BranchId,
            ExpenseCategoryId = request.CategoryId,
            SupplierId = request.SupplierId,
            EventId = request.EventId,
            ExpenseDate = _clock.UtcNow,
            DueDate = request.DueDate,
            Amount = request.Amount,
            PaidAmount = 0m,
            VatRate = request.VatRate,
            VatAmount = decimal.Round(request.Amount * request.VatRate / (100m + request.VatRate), 2),
            Currency = request.Currency,
            ExchangeRate = request.ExchangeRate,
            AmountTry = TryAmount(request.Amount, request.ExchangeRate),
            Status = PaymentStatus.Pending,
            ExpenseType = request.ExpenseType,
            IsRecurring = request.IsRecurring,
            DocumentNumber = request.DocumentNumber?.Trim(),
            Description = request.Description?.Trim(),
        };
        db.Expenses.Add(expense);
        if (expense.SupplierId.HasValue)
            db.SupplierTransactions.Add(SupplierMovement(expense, 0m, expense.Amount,
                "Tedarikçiye gider borcu"));
        db.AuditLogs.Add(Audit(user, nameof(Expense), expense.Id, "EXPENSE_CREATED"));
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return expense.Id;
    }

    public async Task PayExpenseAsync(PayExpense request, CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceManage);
        ArgumentNullException.ThrowIfNull(request);
        ValidateMoney(request.Amount);
        if (request.OperationId == Guid.Empty)
            throw new ArgumentException("İşlem anahtarı boş olamaz.");
        var (scope, db) = Context();
        await using var disposal = scope;
        await using var tx = await BeginAsync(db, ct);
        await LockAsync(db, OperationLock(request.OperationId), ct);
        await LockAsync(db, OperationLock(request.ExpenseId), ct);
        var duplicate = await db.Set<FinancialTransaction>()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == request.OperationId, ct);
        if (duplicate is not null)
        {
            if (duplicate.BranchId != user.BranchId || duplicate.SourceType != nameof(Expense) ||
                duplicate.SourceId != request.ExpenseId || duplicate.AccountId != request.AccountId ||
                duplicate.AccountType != request.AccountType || duplicate.Amount != -request.Amount)
                throw new InvalidOperationException("İşlem anahtarı farklı bir ödemede kullanılmış.");
            return;
        }
        var expense = await db.Expenses.SingleAsync(x => x.Id == request.ExpenseId &&
            x.BranchId == user.BranchId, ct);
        if (expense.IsCancelled) throw new InvalidOperationException("İptal edilmiş gider ödenemez.");
        if (request.Amount > expense.Amount - expense.PaidAmount)
            throw new InvalidOperationException("Ödeme, giderin kalan tutarını aşamaz.");
        var account = await GetAccountAsync(db, user.BranchId, request.AccountId,
            request.AccountType, ct);
        RequireActive(account);
        if (account.Currency != expense.Currency)
            throw new InvalidOperationException("Ödeme hesabı giderle aynı para biriminde olmalıdır.");
        await LockAsync(db, AccountLock(user.BranchId, account.Type, account.Currency), ct);
        var movement = Movement(user, account, FinancialTransactionType.Expense,
            -request.Amount, expense.ExchangeRate, nameof(Expense), expense.Id,
            _clock.UtcNow, expense.Description, request.OperationId);
        db.Set<FinancialTransaction>().Add(movement);
        expense.PaidAmount += request.Amount;
        expense.PaidAt = _clock.UtcNow;
        expense.FinancialAccountId = account.Id;
        expense.Status = expense.PaidAmount == expense.Amount ? PaymentStatus.Paid : PaymentStatus.PartiallyPaid;
        if (expense.SupplierId.HasValue)
            db.SupplierTransactions.Add(SupplierMovement(expense, request.Amount, 0m,
                "Tedarikçiye ödeme"));
        db.AuditLogs.Add(Audit(user, nameof(Expense), expense.Id, "EXPENSE_PAID"));
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
    }

    public async Task CancelExpenseAsync(Guid expenseId, string reason, CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceManage);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var (scope, db) = Context();
        await using var disposal = scope;
        await using var tx = await BeginAsync(db, ct);
        await LockAsync(db, OperationLock(expenseId), ct);
        var expense = await db.Expenses.SingleAsync(x => x.Id == expenseId &&
            x.BranchId == user.BranchId, ct);
        if (expense.IsCancelled) return;
        var movements = await db.Set<FinancialTransaction>()
            .Where(x => x.SourceType == nameof(Expense) && x.SourceId == expenseId &&
                x.TransactionType == FinancialTransactionType.Expense).ToListAsync(ct);
        foreach (var movement in movements)
        {
            var account = await GetAccountAsync(db, user.BranchId, movement.AccountId,
                movement.AccountType, ct);
            await LockAsync(db, AccountLock(user.BranchId, account.Type, account.Currency), ct);
            var reversal = Movement(user, account, FinancialTransactionType.Reversal,
                -movement.Amount, movement.ExchangeRate, nameof(Expense), expense.Id,
                _clock.UtcNow, reason.Trim());
            reversal.ReversalTransactionId = movement.Id;
            db.Set<FinancialTransaction>().Add(reversal);
        }
        if (expense.SupplierId.HasValue)
        {
            var linked = await db.SupplierTransactions.Where(x => x.SupplierId == expense.SupplierId &&
                x.DocumentNumber == ExpenseLedgerKey(expenseId) && !x.IsCancelled).ToListAsync(ct);
            foreach (var entry in linked)
                db.SupplierTransactions.Add(SupplierMovement(expense, entry.Credit,
                    entry.Debit, $"İptal: {reason.Trim()}"));
        }
        expense.IsCancelled = true;
        expense.Status = PaymentStatus.Cancelled;
        db.AuditLogs.Add(Audit(user, nameof(Expense), expense.Id, "EXPENSE_CANCELLED"));
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
    }

    private SupplierTransaction SupplierMovement(Expense expense, decimal debit, decimal credit,
        string description) => new()
        {
            BranchId = expense.BranchId,
            SupplierId = expense.SupplierId!.Value,
            EventId = expense.EventId,
            TransactionDate = _clock.UtcNow,
            Debit = debit,
            Credit = credit,
            Currency = expense.Currency,
            ExchangeRate = expense.ExchangeRate,
            DocumentNumber = ExpenseLedgerKey(expense.Id),
            Description = description,
        };

    private static string ExpenseLedgerKey(Guid expenseId) => $"EXP:{expenseId:N}";
}
