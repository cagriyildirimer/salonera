using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Application.Common.Security;
using Salonera.Application.Common.Time;
using Salonera.Application.Features.Finance;
using Salonera.Domain.Entities;
using Salonera.Domain.Enums;
using Salonera.Infrastructure.Persistence;

namespace Salonera.Infrastructure.Features;

public sealed partial class FinanceService : IFinanceService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUser _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IClock _clock;

    public FinanceService(IServiceScopeFactory scopeFactory, ICurrentUser currentUser,
        IAuthorizationService authorization, IClock clock)
    {
        _scopeFactory = scopeFactory;
        _currentUser = currentUser;
        _authorization = authorization;
        _clock = clock;
    }

    private AuthenticatedUser User(string permission)
    {
        _authorization.EnsurePermission(permission);
        return _currentUser.User ?? throw new AuthorizationException("Bu işlem için giriş yapmalısınız.");
    }

    private (AsyncServiceScope Scope, SaloneraDbContext Db) Context()
    {
        var scope = _scopeFactory.CreateAsyncScope();
        return (scope, scope.ServiceProvider.GetRequiredService<SaloneraDbContext>());
    }

    private static void ValidateMoney(decimal amount, string name = "Tutar")
    {
        if (amount <= 0 || amount != decimal.Round(amount, 2))
            throw new ArgumentException($"{name} sıfırdan büyük ve iki ondalıklı olmalıdır.");
    }

    private static void ValidateRate(CurrencyCode currency, decimal rate)
    {
        if (rate <= 0 || (currency == CurrencyCode.Try && rate != 1m))
            throw new ArgumentException("Döviz kuru geçersiz. TL işlemlerinde kur 1 olmalıdır.");
    }

    private static decimal TryAmount(decimal amount, decimal rate) => decimal.Round(amount * rate, 2);

    private static void ValidateVat(decimal rate)
    {
        if (rate < 0 || rate > 100)
            throw new ArgumentException("KDV oranı 0 ile 100 arasında olmalıdır.");
    }

    private static AuditLog Audit(AuthenticatedUser user, string entity, Guid id, string action) => new()
    {
        BranchId = user.BranchId,
        UserId = user.Id,
        EntityName = entity,
        EntityId = id.ToString(),
        Action = action,
        Succeeded = true,
    };

    private static async Task<IDbContextTransaction?> BeginAsync(SaloneraDbContext db, CancellationToken ct) =>
        db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;

    private static async Task LockAsync(SaloneraDbContext db, string resource, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return;
        await db.Database.ExecuteSqlInterpolatedAsync($$"""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource={{resource}}, @LockMode='Exclusive',
                @LockOwner='Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51003, 'Finansal işlem kilidi alınamadı.', 1;
            """, ct);
    }

    private static string AccountLock(Guid branchId, AccountType type, CurrencyCode currency) =>
        $"Salonera:Account:{branchId:N}:{type}:{currency}";

    private static string OperationLock(Guid operationId) => $"Salonera:Finance:{operationId:N}";

    private sealed record Account(Guid Id, AccountType Type, CurrencyCode Currency,
        decimal OpeningBalance, bool IsActive);

    private static async Task<Account> GetAccountAsync(SaloneraDbContext db, Guid branchId,
        Guid accountId, AccountType type, CancellationToken ct)
    {
        if (type == AccountType.Cash)
        {
            var account = await db.CashAccounts.SingleOrDefaultAsync(x =>
                x.Id == accountId && x.BranchId == branchId, ct)
                ?? throw new InvalidOperationException("Kasa hesabı bulunamadı.");
            return new Account(account.Id, type, account.Currency, account.OpeningBalance, account.IsActive);
        }
        var bank = await db.BankAccounts.SingleOrDefaultAsync(x =>
            x.Id == accountId && x.BranchId == branchId, ct)
            ?? throw new InvalidOperationException("Banka hesabı bulunamadı.");
        return new Account(bank.Id, type, bank.Currency, bank.OpeningBalance, bank.IsActive);
    }

    private static void RequireActive(Account account)
    {
        if (!account.IsActive) throw new InvalidOperationException("Finans hesabı aktif değil.");
    }

    private static FinancialTransaction Movement(AuthenticatedUser user, Account account,
        FinancialTransactionType type, decimal amount, decimal exchangeRate,
        string sourceType, Guid sourceId, DateTimeOffset date,
        string? description = null, Guid? operationId = null) => new()
        {
            BranchId = user.BranchId,
            AccountType = account.Type,
            AccountId = account.Id,
            TransactionType = type,
            TransactionDate = date,
            Amount = amount,
            Currency = account.Currency,
            ExchangeRate = exchangeRate,
            AmountTry = TryAmount(amount, exchangeRate),
            SourceType = sourceType,
            SourceId = sourceId,
            Description = description,
            IdempotencyKey = operationId,
        };

    private static async Task<decimal> BalanceAsync(SaloneraDbContext db, Account account,
        CancellationToken ct)
    {
        var movementSum = await db.Set<FinancialTransaction>()
            .Where(x => x.AccountId == account.Id && x.AccountType == account.Type && !x.IsCancelled)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        return account.OpeningBalance + movementSum;
    }
}
