using Microsoft.EntityFrameworkCore;
using Salonera.Application.Common.Security;
using Salonera.Application.Features.Finance;
using Salonera.Domain.Entities;
using Salonera.Domain.Enums;

namespace Salonera.Infrastructure.Features;

public sealed partial class FinanceService
{
    public async Task<IReadOnlyList<FinanceCategoryItem>> ListIncomeCategoriesAsync(CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceView);
        var (scope, db) = Context();
        await using var disposal = scope;
        return await db.IncomeCategories.AsNoTracking()
            .Where(x => x.BranchId == user.BranchId && x.IsActive)
            .OrderBy(x => x.Name).Select(x => new FinanceCategoryItem(x.Id, x.Name))
            .ToListAsync(ct);
    }

    public async Task<Guid> CreateIncomeCategoryAsync(string name, CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceManage);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        name = name.Trim();
        if (name.Length > 100) throw new ArgumentException("Kategori adı çok uzun.");
        var (scope, db) = Context();
        await using var disposal = scope;
        if (await db.IncomeCategories.AnyAsync(x => x.BranchId == user.BranchId && x.Name == name, ct))
            throw new InvalidOperationException("Bu gelir kategorisi zaten var.");
        var category = new IncomeCategory { BranchId = user.BranchId, Name = name };
        db.IncomeCategories.Add(category);
        db.AuditLogs.Add(Audit(user, nameof(IncomeCategory), category.Id, "INCOME_CATEGORY_CREATED"));
        await db.SaveChangesAsync(ct);
        return category.Id;
    }

    public async Task<IReadOnlyList<IncomeItem>> ListIncomesAsync(CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceView);
        var (scope, db) = Context();
        await using var disposal = scope;
        return await db.Incomes.AsNoTracking().Where(x => x.BranchId == user.BranchId)
            .OrderByDescending(x => x.IncomeDate)
            .Select(x => new IncomeItem(x.Id, x.IncomeDate, x.Amount,
                x.Currency, x.IncomeCategory.Name, x.Description, x.IsCancelled))
            .ToListAsync(ct);
    }

    public async Task<Guid> CreateIncomeAsync(NewIncome request, CancellationToken ct = default)
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
        var existing = await db.Incomes.SingleOrDefaultAsync(x => x.Id == request.OperationId, ct);
        if (existing is not null)
        {
            if (existing.BranchId != user.BranchId || existing.Amount != request.Amount ||
                existing.FinancialAccountId != request.AccountId || existing.IncomeCategoryId != request.CategoryId ||
                existing.Currency != request.Currency || existing.ExchangeRate != request.ExchangeRate)
                throw new InvalidOperationException("İşlem anahtarı farklı bir gelirde kullanılmış.");
            return existing.Id;
        }
        if (!await db.IncomeCategories.AnyAsync(x => x.Id == request.CategoryId &&
            x.BranchId == user.BranchId && x.IsActive, ct))
            throw new InvalidOperationException("Gelir kategorisi bulunamadı.");
        if (request.EventId.HasValue && !await db.Events.AnyAsync(x => x.Id == request.EventId &&
            x.BranchId == user.BranchId, ct))
            throw new InvalidOperationException("Organizasyon bu şubeye ait değil.");
        if (request.CustomerId.HasValue && !await db.Customers.AnyAsync(x => x.Id == request.CustomerId &&
            x.BranchId == user.BranchId, ct))
            throw new InvalidOperationException("Müşteri bu şubeye ait değil.");
        var account = await GetAccountAsync(db, user.BranchId, request.AccountId,
            request.AccountType, ct);
        RequireActive(account);
        if (account.Currency != request.Currency)
            throw new InvalidOperationException("Gelir para birimi hesapla aynı olmalıdır.");
        await LockAsync(db, AccountLock(user.BranchId, account.Type, account.Currency), ct);
        var income = new Income
        {
            Id = request.OperationId,
            BranchId = user.BranchId,
            IncomeCategoryId = request.CategoryId,
            EventId = request.EventId,
            CustomerId = request.CustomerId,
            IncomeDate = _clock.UtcNow,
            Amount = request.Amount,
            VatRate = request.VatRate,
            VatAmount = decimal.Round(request.Amount * request.VatRate / (100m + request.VatRate), 2),
            Currency = request.Currency,
            ExchangeRate = request.ExchangeRate,
            AmountTry = TryAmount(request.Amount, request.ExchangeRate),
            PaymentMethod = account.Type == AccountType.Cash ? PaymentMethod.Cash : PaymentMethod.Bank,
            FinancialAccountId = account.Id,
            Description = request.Description?.Trim(),
            DocumentNumber = request.DocumentNumber?.Trim(),
        };
        db.Incomes.Add(income);
        db.Set<FinancialTransaction>().Add(Movement(user, account,
            FinancialTransactionType.Income, income.Amount, income.ExchangeRate,
            nameof(Income), income.Id, _clock.UtcNow, income.Description, request.OperationId));
        db.AuditLogs.Add(Audit(user, nameof(Income), income.Id, "INCOME_CREATED"));
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return income.Id;
    }

    public async Task CancelIncomeAsync(Guid incomeId, string reason, CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceManage);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var (scope, db) = Context();
        await using var disposal = scope;
        await using var tx = await BeginAsync(db, ct);
        await LockAsync(db, OperationLock(incomeId), ct);
        var income = await db.Incomes.SingleAsync(x => x.Id == incomeId && x.BranchId == user.BranchId, ct);
        if (income.IsCancelled) return;
        var original = await db.Set<FinancialTransaction>().SingleAsync(x =>
            x.SourceType == nameof(Income) && x.SourceId == incomeId &&
            x.TransactionType == FinancialTransactionType.Income, ct);
        var account = await GetAccountAsync(db, user.BranchId, original.AccountId,
            original.AccountType, ct);
        await LockAsync(db, AccountLock(user.BranchId, account.Type, account.Currency), ct);
        income.IsCancelled = true;
        var reversal = Movement(user, account, FinancialTransactionType.Reversal,
            -original.Amount, original.ExchangeRate, nameof(Income), income.Id,
            _clock.UtcNow, reason.Trim());
        reversal.ReversalTransactionId = original.Id;
        db.Set<FinancialTransaction>().Add(reversal);
        db.AuditLogs.Add(Audit(user, nameof(Income), income.Id, "INCOME_CANCELLED"));
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
    }
}
