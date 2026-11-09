using Microsoft.EntityFrameworkCore;
using Salonera.Application.Common.Security;
using Salonera.Application.Features.Finance;
using Salonera.Domain.Entities;
using Salonera.Domain.Enums;

namespace Salonera.Infrastructure.Features;

public sealed partial class FinanceService
{
    public async Task<IReadOnlyList<FinancialAccountItem>> ListAccountsAsync(CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceView);
        var (scope, db) = Context();
        await using var disposal = scope;
        var cash = await db.CashAccounts.AsNoTracking().Where(x => x.BranchId == user.BranchId)
            .ToListAsync(ct);
        var banks = await db.BankAccounts.AsNoTracking().Where(x => x.BranchId == user.BranchId)
            .ToListAsync(ct);
        var movements = await db.Set<FinancialTransaction>().AsNoTracking()
            .Where(x => x.BranchId == user.BranchId && !x.IsCancelled).ToListAsync(ct);
        var sums = movements.GroupBy(x => (x.AccountType, x.AccountId))
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        return cash.Select(x => new FinancialAccountItem(x.Id, AccountType.Cash, x.Code,
                x.Name, x.Currency, x.OpeningBalance + sums.GetValueOrDefault((AccountType.Cash, x.Id)),
                x.IsActive))
            .Concat(banks.Select(x => new FinancialAccountItem(x.Id, AccountType.Bank, x.Code,
                x.Name, x.Currency, x.OpeningBalance + sums.GetValueOrDefault((AccountType.Bank, x.Id)),
                x.IsActive)))
            .OrderBy(x => x.Type).ThenBy(x => x.Code).ToArray();
    }

    public async Task<IReadOnlyList<FinancialMovementItem>> ListMovementsAsync(Guid accountId,
        AccountType type, CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceView);
        var (scope, db) = Context();
        await using var disposal = scope;
        await GetAccountAsync(db, user.BranchId, accountId, type, ct);
        return await db.Set<FinancialTransaction>().AsNoTracking()
            .Where(x => x.BranchId == user.BranchId && x.AccountType == type &&
                x.AccountId == accountId && !x.IsCancelled)
            .OrderByDescending(x => x.TransactionDate)
            .Select(x => new FinancialMovementItem(x.Id, x.TransactionDate,
                x.TransactionType, x.Amount, x.Currency, x.Description))
            .ToListAsync(ct);
    }

    public async Task<Guid> CreateAccountAsync(NewFinancialAccount request, CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceManage);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) ||
            request.OpeningBalance < 0 || request.OpeningBalance != decimal.Round(request.OpeningBalance, 2))
            throw new ArgumentException("Hesap kodu, adı ve açılış bakiyesi geçerli olmalıdır.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (code.Length > 30 || request.Name.Trim().Length > 100)
            throw new ArgumentException("Hesap kodu veya adı çok uzun.");
        var (scope, db) = Context();
        await using var disposal = scope;
        await using var tx = await BeginAsync(db, ct);
        await LockAsync(db, AccountLock(user.BranchId, request.Type, request.Currency), ct);
        Guid id;
        if (request.Type == AccountType.Cash)
        {
            if (await db.CashAccounts.AnyAsync(x => x.BranchId == user.BranchId && x.Code == code, ct))
                throw new InvalidOperationException("Bu kasa kodu zaten kullanılıyor.");
            var account = new CashAccount
            {
                BranchId = user.BranchId,
                Code = code,
                Name = request.Name.Trim(),
                Currency = request.Currency,
                OpeningBalance = request.OpeningBalance,
            };
            db.CashAccounts.Add(account);
            id = account.Id;
        }
        else
        {
            if (await db.BankAccounts.AnyAsync(x => x.BranchId == user.BranchId && x.Code == code, ct))
                throw new InvalidOperationException("Bu banka kodu zaten kullanılıyor.");
            var account = new BankAccount
            {
                BranchId = user.BranchId,
                Code = code,
                Name = request.Name.Trim(),
                Currency = request.Currency,
                OpeningBalance = request.OpeningBalance,
                BankName = request.BankName?.Trim(),
                Iban = request.Iban?.Trim(),
            };
            db.BankAccounts.Add(account);
            id = account.Id;
        }
        db.AuditLogs.Add(Audit(user, request.Type.ToString(), id, "FINANCIAL_ACCOUNT_CREATED"));
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return id;
    }

    public async Task<Guid> TransferAsync(NewTransfer request, CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceManage);
        ArgumentNullException.ThrowIfNull(request);
        ValidateMoney(request.Amount);
        if (request.OperationId == Guid.Empty || request.FromAccountId == request.ToAccountId)
            throw new ArgumentException("Farklı kaynak ve hedef hesap seçin.");
        var (scope, db) = Context();
        await using var disposal = scope;
        await using var tx = await BeginAsync(db, ct);
        await LockAsync(db, OperationLock(request.OperationId), ct);
        var existing = await db.Transfers.SingleOrDefaultAsync(x => x.Id == request.OperationId, ct);
        if (existing is not null)
        {
            if (existing.BranchId != user.BranchId || existing.FromAccountId != request.FromAccountId ||
                existing.ToAccountId != request.ToAccountId || existing.Amount != request.Amount ||
                existing.ExchangeRate != request.ExchangeRate)
                throw new InvalidOperationException("İşlem anahtarı farklı bir virmanda kullanılmış.");
            return existing.Id;
        }
        var source = await GetAccountAsync(db, user.BranchId, request.FromAccountId,
            request.FromAccountType, ct);
        var target = await GetAccountAsync(db, user.BranchId, request.ToAccountId,
            request.ToAccountType, ct);
        RequireActive(source);
        RequireActive(target);
        if (source.Currency != target.Currency)
            throw new InvalidOperationException("Virman hesapları aynı para biriminde olmalıdır.");
        ValidateRate(source.Currency, request.ExchangeRate);
        var resources = new[] { AccountLock(user.BranchId, source.Type, source.Currency),
            AccountLock(user.BranchId, target.Type, target.Currency) };
        foreach (var resource in resources.Distinct().Order(StringComparer.Ordinal))
            await LockAsync(db, resource, ct);
        if (await BalanceAsync(db, source, ct) < request.Amount)
            throw new InvalidOperationException("Kaynak hesap bakiyesi yetersiz.");
        var transfer = new Transfer
        {
            Id = request.OperationId,
            BranchId = user.BranchId,
            FromAccountType = source.Type,
            FromAccountId = source.Id,
            ToAccountType = target.Type,
            ToAccountId = target.Id,
            TransferDate = _clock.UtcNow,
            Amount = request.Amount,
            Currency = source.Currency,
            ExchangeRate = request.ExchangeRate,
            Description = request.Description?.Trim(),
        };
        db.Transfers.Add(transfer);
        db.Set<FinancialTransaction>().Add(Movement(user, source,
            FinancialTransactionType.TransferOut, -request.Amount, request.ExchangeRate,
            nameof(Transfer), transfer.Id, _clock.UtcNow, transfer.Description, request.OperationId));
        db.Set<FinancialTransaction>().Add(Movement(user, target,
            FinancialTransactionType.TransferIn, request.Amount, request.ExchangeRate,
            nameof(Transfer), transfer.Id, _clock.UtcNow, transfer.Description));
        db.AuditLogs.Add(Audit(user, nameof(Transfer), transfer.Id, "TRANSFER_CREATED"));
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return transfer.Id;
    }

    public async Task<IReadOnlyList<TransferItem>> ListTransfersAsync(CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceView);
        var (scope, db) = Context();
        await using var disposal = scope;
        var transfers = await db.Transfers.AsNoTracking().Where(x => x.BranchId == user.BranchId)
            .OrderByDescending(x => x.TransferDate).ToListAsync(ct);
        var names = (await ListAccountsAsync(ct)).ToDictionary(x => x.Id, x => x.Name);
        return transfers.Select(x => new TransferItem(x.Id, x.TransferDate,
            names.GetValueOrDefault(x.FromAccountId, "Silinmiş hesap"),
            names.GetValueOrDefault(x.ToAccountId, "Silinmiş hesap"),
            x.Amount, x.Currency, x.IsCancelled, x.Description)).ToArray();
    }

    public async Task CancelTransferAsync(Guid transferId, string reason, CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceManage);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var (scope, db) = Context();
        await using var disposal = scope;
        await using var tx = await BeginAsync(db, ct);
        await LockAsync(db, OperationLock(transferId), ct);
        var transfer = await db.Transfers.SingleAsync(x => x.Id == transferId &&
            x.BranchId == user.BranchId, ct);
        if (transfer.IsCancelled) return;
        var original = await db.Set<FinancialTransaction>()
            .Where(x => x.SourceType == nameof(Transfer) && x.SourceId == transferId &&
                x.TransactionType != FinancialTransactionType.Reversal).ToListAsync(ct);
        var accounts = new Dictionary<Guid, Account>();
        foreach (var movement in original)
        {
            var account = await GetAccountAsync(db, user.BranchId, movement.AccountId,
                movement.AccountType, ct);
            accounts[account.Id] = account;
        }
        foreach (var resource in accounts.Values.Select(x => AccountLock(user.BranchId, x.Type, x.Currency))
            .Distinct().Order(StringComparer.Ordinal))
            await LockAsync(db, resource, ct);
        var destination = accounts.GetValueOrDefault(transfer.ToAccountId);
        if (destination is not null && await BalanceAsync(db, destination, ct) < transfer.Amount)
            throw new InvalidOperationException("Virman iptali için hedef hesap bakiyesi yetersiz.");
        foreach (var movement in original)
        {
            db.Set<FinancialTransaction>().Add(new FinancialTransaction
            {
                BranchId = user.BranchId,
                AccountType = movement.AccountType,
                AccountId = movement.AccountId,
                TransactionType = FinancialTransactionType.Reversal,
                TransactionDate = _clock.UtcNow,
                Amount = -movement.Amount,
                Currency = movement.Currency,
                ExchangeRate = movement.ExchangeRate,
                AmountTry = -movement.AmountTry,
                SourceType = nameof(Transfer),
                SourceId = transferId,
                ReversalTransactionId = movement.Id,
                Description = reason.Trim(),
            });
        }
        transfer.IsCancelled = true;
        db.AuditLogs.Add(Audit(user, nameof(Transfer), transfer.Id, "TRANSFER_CANCELLED"));
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
    }
}
