using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Application.Common.Security;
using Salonera.Application.Common.Time;
using Salonera.Application.Features.Payments;
using Salonera.Domain.Entities;
using Salonera.Domain.Enums;
using Salonera.Infrastructure.Persistence;

namespace Salonera.Infrastructure.Features;

public sealed class PaymentService(
    IServiceScopeFactory scopeFactory, ICurrentUser currentUser,
    IAuthorizationService authorization, IClock clock) : IPaymentService
{
    public async Task<IReadOnlyList<ContractPaymentListItem>> ListContractsAsync(CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(Permissions.FinanceView);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        return await db.Contracts.AsNoTracking().Where(x => x.BranchId == user.BranchId)
            .OrderByDescending(x => x.ContractDate)
            .Select(x => new ContractPaymentListItem(x.Id, x.ContractNumber,
                x.Event.Customer.FirstName + " " + x.Event.Customer.LastName,
                x.NetAmount, x.Currency)).ToListAsync(cancellationToken);
    }

    public async Task<ContractBalance> GetBalanceAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(Permissions.FinanceView);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var contract = await ContractAsync(db, contractId, user.BranchId, cancellationToken);
        var receipts = await db.Collections.AsNoTracking()
            .Where(x => x.ContractId == contractId && x.BranchId == user.BranchId)
            .OrderByDescending(x => x.CollectedAt).ToListAsync(cancellationToken);
        var refunds = await db.Refunds.AsNoTracking()
            .Where(x => x.ContractId == contractId && x.BranchId == user.BranchId && !x.IsCancelled)
            .ToListAsync(cancellationToken);
        var plans = await db.PaymentPlans.AsNoTracking().Where(x => x.ContractId == contractId)
            .OrderBy(x => x.DueDate).ToListAsync(cancellationToken);
        var paid = receipts.Where(x => !x.IsCancelled).Sum(x => x.Amount);
        var cancelledReceiptIds = receipts.Where(x => x.IsCancelled).Select(x => x.Id).ToHashSet();
        var returned = refunds.Where(x => !x.CollectionId.HasValue ||
            !cancelledReceiptIds.Contains(x.CollectionId.Value)).Sum(x => x.Amount);
        var refundByReceipt = refunds.Where(x => x.CollectionId.HasValue)
            .GroupBy(x => x.CollectionId!.Value).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        var netByPlan = receipts.Where(x => !x.IsCancelled && x.PaymentPlanId.HasValue)
            .GroupBy(x => x.PaymentPlanId!.Value).ToDictionary(x => x.Key,
                x => x.Sum(y => y.Amount - refundByReceipt.GetValueOrDefault(y.Id)));
        var today = DateOnly.FromDateTime(clock.TurkeyNow.Date);
        var installmentItems = plans.Select(x =>
        {
            var amountPaid = netByPlan.GetValueOrDefault(x.Id);
            var status = amountPaid >= x.Amount ? PaymentStatus.Paid
                : x.DueDate < today ? PaymentStatus.Overdue
                : amountPaid > 0 ? PaymentStatus.PartiallyPaid : PaymentStatus.Pending;
            return new InstallmentItem(x.Id, x.DueDate, x.Amount, amountPaid, status);
        }).ToArray();
        return new ContractBalance(contractId, contract.ContractNumber,
            contract.Event.Customer.FirstName + " " + contract.Event.Customer.LastName,
            contract.NetAmount, paid, returned, contract.NetAmount - paid + returned,
            contract.Currency, installmentItems,
            receipts.Select(x => new ReceiptItem(x.Id, x.ReceiptNumber, x.CollectedAt,
                x.Amount, refundByReceipt.GetValueOrDefault(x.Id), x.Currency,
                x.PaymentMethod, x.IsCancelled, x.PaymentPlanId)).ToArray());
    }

    public async Task<ReceiptDocument> GetReceiptAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(Permissions.FinanceView);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var contractId = await db.Collections.AsNoTracking()
            .Where(x => x.Id == receiptId && x.BranchId == user.BranchId)
            .Select(x => x.ContractId).SingleAsync(cancellationToken);
        var balance = await GetBalanceAsync(contractId, cancellationToken);
        return new ReceiptDocument(balance, balance.Receipts.Single(x => x.Id == receiptId));
    }

    public async Task SetPlanAsync(Guid contractId, IReadOnlyList<NewInstallment> installments,
        CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(Permissions.FinanceManage);
        ArgumentNullException.ThrowIfNull(installments);
        if (installments.Count == 0 || installments.Any(x => x.Amount <= 0 || !Money(x.Amount)))
            throw new ArgumentException("Taksit tutarları sıfırdan büyük ve iki ondalıklı olmalıdır.");
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        await using var tx = await BeginAsync(db, cancellationToken);
        await LockAsync(db, contractId, cancellationToken);
        var contract = await ContractAsync(db, contractId, user.BranchId, cancellationToken);
        if (installments.Sum(x => x.Amount) != contract.NetAmount)
            throw new InvalidOperationException("Taksit toplamı sözleşme tutarına eşit olmalıdır.");
        if (await db.PaymentPlans.AnyAsync(x => x.ContractId == contractId, cancellationToken) ||
            await db.Collections.AnyAsync(x => x.ContractId == contractId && !x.IsCancelled, cancellationToken))
            throw new InvalidOperationException("Ödeme planı veya tahsilat bulunan sözleşmenin planı değiştirilemez.");
        foreach (var item in installments)
            db.PaymentPlans.Add(new PaymentPlan
            {
                ContractId = contractId,
                DueDate = item.DueDate,
                Amount = item.Amount,
                Currency = contract.Currency,
                Status = PaymentStatus.Pending
            });
        db.AuditLogs.Add(Audit(user, nameof(PaymentPlan), contractId, "PAYMENT_PLAN_CREATED"));
        await db.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);
    }

    public async Task<Guid> CollectAsync(CollectPaymentRequest request, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(Permissions.FinanceManage);
        ArgumentNullException.ThrowIfNull(request);
        ValidateAmount(request.Amount);
        if (request.ExchangeRate <= 0 || request.IdempotencyKey == Guid.Empty)
            throw new ArgumentException("Kur ve işlem anahtarı geçerli olmalıdır.");
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        await using var tx = await BeginAsync(db, cancellationToken);
        await LockAsync(db, request.ContractId, cancellationToken);
        var duplicate = await db.Collections.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (duplicate is not null)
        {
            if (duplicate.BranchId != user.BranchId || duplicate.ContractId != request.ContractId ||
                duplicate.Amount != request.Amount || duplicate.PaymentPlanId != request.PaymentPlanId ||
                duplicate.PaymentMethod != request.Method || duplicate.ExchangeRate != request.ExchangeRate ||
                (request.FinancialAccountId.HasValue &&
                 duplicate.FinancialAccountId != request.FinancialAccountId))
                throw new InvalidOperationException("İşlem anahtarı farklı bir tahsilatta kullanılmış.");
            return duplicate.Id;
        }
        var contract = await ContractAsync(db, request.ContractId, user.BranchId, cancellationToken);
        if (contract.Event.Status == EventStatus.Cancelled)
            throw new InvalidOperationException("İptal edilmiş organizasyona tahsilat yapılamaz.");
        var collected = await db.Collections.Where(x => x.ContractId == contract.Id && !x.IsCancelled)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var refunded = await db.Refunds.Where(x => x.ContractId == contract.Id && !x.IsCancelled &&
                (x.CollectionId == null || !x.Collection!.IsCancelled))
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        if (request.Amount > contract.NetAmount - collected + refunded)
            throw new InvalidOperationException("Tahsilat kalan borcu aşamaz.");
        PaymentPlan? plan = null;
        if (request.PaymentPlanId.HasValue)
        {
            plan = await db.PaymentPlans.SingleOrDefaultAsync(x => x.Id == request.PaymentPlanId &&
                x.ContractId == contract.Id, cancellationToken)
                ?? throw new InvalidOperationException("Taksit bu sözleşmeye ait değil.");
            var planCollected = await db.Collections.Where(x => x.PaymentPlanId == plan.Id && !x.IsCancelled)
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
            var planRefunded = await db.Refunds.Where(x => x.Collection != null &&
                x.Collection.PaymentPlanId == plan.Id && !x.Collection.IsCancelled && !x.IsCancelled)
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
            if (request.Amount > plan.Amount - planCollected + planRefunded)
                throw new InvalidOperationException("Tahsilat seçilen taksitin kalan tutarını aşamaz.");
        }
        var account = await AccountAsync(db, user.BranchId, contract.Currency, request.Method,
            request.FinancialAccountId, cancellationToken);
        var amountTry = decimal.Round(request.Amount * request.ExchangeRate, 2);
        var receipt = new PaymentReceipt
        {
            BranchId = user.BranchId,
            ContractId = contract.Id,
            PaymentPlanId = plan?.Id,
            ReceiptNumber = $"THS-{clock.TurkeyNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            CollectedAt = clock.UtcNow,
            Amount = request.Amount,
            Currency = contract.Currency,
            ExchangeRate = contract.Currency == CurrencyCode.Try ? 1m : request.ExchangeRate,
            AmountTry = contract.Currency == CurrencyCode.Try ? request.Amount : amountTry,
            PaymentMethod = request.Method,
            FinancialAccountId = account.Id,
            IdempotencyKey = request.IdempotencyKey,
        };
        db.Collections.Add(receipt);
        db.Set<FinancialTransaction>().Add(Transaction(receipt, account.Type,
            FinancialTransactionType.Collection, receipt.Amount, receipt.AmountTry, receipt.Id));
        if (plan is not null) await UpdatePlanAsync(db, plan, request.Amount, cancellationToken);
        db.AuditLogs.Add(Audit(user, nameof(PaymentReceipt), receipt.Id, "PAYMENT_COLLECTED"));
        await db.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);
        return receipt.Id;
    }

    public async Task CancelReceiptAsync(Guid receiptId, string reason, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(Permissions.FinanceManage);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var receiptContractId = await db.Collections.Where(x => x.Id == receiptId && x.BranchId == user.BranchId)
            .Select(x => x.ContractId).SingleAsync(cancellationToken);
        await using var tx = await BeginAsync(db, cancellationToken);
        await LockAsync(db, receiptContractId, cancellationToken);
        var receipt = await db.Collections.SingleAsync(x => x.Id == receiptId && x.BranchId == user.BranchId,
            cancellationToken);
        if (receipt.IsCancelled) return;
        var refunds = await db.Refunds.Where(x => x.CollectionId == receiptId && !x.IsCancelled)
            .ToListAsync(cancellationToken);
        var remaining = receipt.Amount - refunds.Sum(x => x.Amount);
        receipt.IsCancelled = true;
        receipt.CancellationReason = reason.Trim();
        var original = await db.Set<FinancialTransaction>().SingleAsync(x =>
            x.SourceType == nameof(PaymentReceipt) && x.SourceId == receiptId &&
            x.TransactionType == FinancialTransactionType.Collection, cancellationToken);
        if (remaining > 0m) db.Set<FinancialTransaction>().Add(new FinancialTransaction
        {
            BranchId = user.BranchId,
            AccountType = original.AccountType,
            AccountId = original.AccountId,
            TransactionType = FinancialTransactionType.Reversal,
            TransactionDate = clock.UtcNow,
            Amount = -remaining,
            Currency = original.Currency,
            ExchangeRate = original.ExchangeRate,
            AmountTry = -(original.AmountTry - refunds.Sum(x => x.AmountTry)),
            SourceType = nameof(PaymentReceipt),
            SourceId = receipt.Id,
            ReversalTransactionId = original.Id,
            Description = reason.Trim(),
        });
        if (receipt.PaymentPlanId.HasValue)
        {
            var plan = await db.PaymentPlans.SingleAsync(x => x.Id == receipt.PaymentPlanId, cancellationToken);
            await UpdatePlanAsync(db, plan, -remaining, cancellationToken);
        }
        db.AuditLogs.Add(Audit(user, nameof(PaymentReceipt), receipt.Id, "PAYMENT_CANCELLED"));
        await db.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);
    }

    public async Task<Guid> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(Permissions.FinanceManage);
        ArgumentNullException.ThrowIfNull(request);
        ValidateAmount(request.Amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var receiptContractId = await db.Collections.Where(x => x.Id == request.ReceiptId && x.BranchId == user.BranchId)
            .Select(x => x.ContractId).SingleAsync(cancellationToken);
        await using var tx = await BeginAsync(db, cancellationToken);
        await LockAsync(db, receiptContractId, cancellationToken);
        var receipt = await db.Collections.SingleAsync(x => x.Id == request.ReceiptId &&
            x.BranchId == user.BranchId, cancellationToken);
        if (receipt.IsCancelled) throw new InvalidOperationException("İptal edilmiş tahsilat iade edilemez.");
        var alreadyRefunded = await db.Refunds.Where(x => x.CollectionId == receipt.Id && !x.IsCancelled)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        if (request.Amount > receipt.Amount - alreadyRefunded)
            throw new InvalidOperationException("İade, tahsilatın kalan tutarını aşamaz.");
        var amountTry = decimal.Round(request.Amount * receipt.ExchangeRate, 2);
        var refund = new Refund
        {
            BranchId = user.BranchId,
            ContractId = receipt.ContractId,
            CollectionId = receipt.Id,
            RefundedAt = clock.UtcNow,
            Amount = request.Amount,
            Currency = receipt.Currency,
            ExchangeRate = receipt.ExchangeRate,
            AmountTry = amountTry,
            Reason = request.Reason.Trim(),
            FinancialAccountId = receipt.FinancialAccountId,
        };
        db.Refunds.Add(refund);
        var original = await db.Set<FinancialTransaction>().SingleAsync(x =>
            x.SourceType == nameof(PaymentReceipt) && x.SourceId == receipt.Id &&
            x.TransactionType == FinancialTransactionType.Collection, cancellationToken);
        db.Set<FinancialTransaction>().Add(new FinancialTransaction
        {
            BranchId = user.BranchId,
            AccountType = original.AccountType,
            AccountId = receipt.FinancialAccountId,
            TransactionType = FinancialTransactionType.Refund,
            TransactionDate = clock.UtcNow,
            Amount = -refund.Amount,
            Currency = refund.Currency,
            ExchangeRate = refund.ExchangeRate,
            AmountTry = -refund.AmountTry,
            SourceType = nameof(Refund),
            SourceId = refund.Id,
            Description = refund.Reason,
        });
        if (receipt.PaymentPlanId.HasValue)
        {
            var plan = await db.PaymentPlans.SingleAsync(x => x.Id == receipt.PaymentPlanId, cancellationToken);
            await UpdatePlanAsync(db, plan, -request.Amount, cancellationToken);
        }
        db.AuditLogs.Add(Audit(user, nameof(Refund), refund.Id, "PAYMENT_REFUNDED"));
        await db.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);
        return refund.Id;
    }

    private static async Task<Contract> ContractAsync(SaloneraDbContext db, Guid id, Guid branchId,
        CancellationToken ct) => await db.Contracts.Include(x => x.Event).ThenInclude(x => x.Customer)
        .SingleAsync(x => x.Id == id && x.BranchId == branchId, ct);

    private static async Task UpdatePlanAsync(SaloneraDbContext db, PaymentPlan plan, decimal delta,
        CancellationToken ct)
    {
        var paid = await db.Collections.Where(x => x.PaymentPlanId == plan.Id && !x.IsCancelled)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var refunded = await db.Refunds.Where(x => x.Collection != null &&
            x.Collection.PaymentPlanId == plan.Id && !x.Collection.IsCancelled && !x.IsCancelled)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var net = paid - refunded + delta;
        plan.Status = net >= plan.Amount ? PaymentStatus.Paid
            : net > 0 ? PaymentStatus.PartiallyPaid : PaymentStatus.Pending;
    }

    private static async Task<(Guid Id, AccountType Type)> AccountAsync(SaloneraDbContext db, Guid branchId,
        CurrencyCode currency, PaymentMethod method, Guid? requestedId, CancellationToken ct)
    {
        var type = method == PaymentMethod.Cash ? AccountType.Cash : AccountType.Bank;
        await LockAccountAsync(db, branchId, type, currency, ct);
        if (requestedId.HasValue)
        {
            if (type == AccountType.Cash)
            {
                var selected = await db.CashAccounts.SingleOrDefaultAsync(x =>
                    x.Id == requestedId && x.BranchId == branchId && x.Currency == currency && x.IsActive, ct);
                if (selected is null) throw new InvalidOperationException("Seçilen kasa hesabı uygun değil.");
                return (selected.Id, type);
            }
            var bank = await db.BankAccounts.SingleOrDefaultAsync(x =>
                x.Id == requestedId && x.BranchId == branchId && x.Currency == currency && x.IsActive, ct);
            if (bank is null) throw new InvalidOperationException("Seçilen banka hesabı uygun değil.");
            return (bank.Id, type);
        }
        var code = type == AccountType.Cash ? $"KASA-{currency}" : $"BANKA-{currency}";
        if (type == AccountType.Cash)
        {
            var account = await db.CashAccounts.SingleOrDefaultAsync(x => x.BranchId == branchId &&
                x.Code == code, ct);
            if (account is null)
            {
                account = new CashAccount
                {
                    BranchId = branchId,
                    Code = code,
                    Name = $"Varsayılan Kasa ({currency})",
                    Currency = currency
                };
                db.CashAccounts.Add(account);
            }
            if (!account.IsActive) throw new InvalidOperationException("Kasa hesabı aktif değil.");
            return (account.Id, type);
        }
        else
        {
            var account = await db.BankAccounts.SingleOrDefaultAsync(x => x.BranchId == branchId &&
                x.Code == code, ct);
            if (account is null)
            {
                account = new BankAccount
                {
                    BranchId = branchId,
                    Code = code,
                    Name = $"Varsayılan Banka ({currency})",
                    Currency = currency
                };
                db.BankAccounts.Add(account);
            }
            if (!account.IsActive) throw new InvalidOperationException("Banka hesabı aktif değil.");
            return (account.Id, type);
        }
    }

    private FinancialTransaction Transaction(PaymentReceipt receipt, AccountType type,
        FinancialTransactionType transactionType, decimal amount, decimal amountTry, Guid sourceId) => new()
        {
            BranchId = receipt.BranchId,
            AccountType = type,
            AccountId = receipt.FinancialAccountId,
            TransactionType = transactionType,
            TransactionDate = clock.UtcNow,
            Amount = amount,
            Currency = receipt.Currency,
            ExchangeRate = receipt.ExchangeRate,
            AmountTry = amountTry,
            SourceType = nameof(PaymentReceipt),
            SourceId = sourceId,
            DocumentNumber = receipt.ReceiptNumber,
            IdempotencyKey = receipt.IdempotencyKey,
        };

    private static async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginAsync(
        SaloneraDbContext db, CancellationToken ct) => db.Database.IsRelational()
        ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;

    private static async Task LockAsync(SaloneraDbContext db, Guid contractId, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return;
        var resource = $"Salonera:Contract:{contractId:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($$"""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource={{resource}}, @LockMode='Exclusive',
                @LockOwner='Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51001, 'Sözleşme kilidi alınamadı.', 1;
            """, ct);
    }

    private static async Task LockAccountAsync(SaloneraDbContext db, Guid branchId, AccountType type,
        CurrencyCode currency, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return;
        var resource = $"Salonera:Account:{branchId:N}:{type}:{currency}";
        await db.Database.ExecuteSqlInterpolatedAsync($$"""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource={{resource}}, @LockMode='Exclusive',
                @LockOwner='Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51002, 'Hesap kilidi alınamadı.', 1;
            """, ct);
    }

    private AuthenticatedUser RequiredUser() => currentUser.User
        ?? throw new AuthorizationException("Bu işlem için giriş yapmalısınız.");

    private static AuditLog Audit(AuthenticatedUser user, string entity, Guid id, string action) => new()
    {
        BranchId = user.BranchId,
        UserId = user.Id,
        EntityName = entity,
        EntityId = id.ToString(),
        Action = action,
        Succeeded = true,
    };

    private static bool Money(decimal value) => decimal.Round(value, 2) == value;
    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0 || !Money(amount))
            throw new ArgumentException("Tutar sıfırdan büyük ve iki ondalıklı olmalıdır.");
    }
}
