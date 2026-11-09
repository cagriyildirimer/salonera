using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Application.Common.Security;
using Salonera.Application.Features.Reports;
using Salonera.Domain.Entities;
using Salonera.Domain.Enums;
using Salonera.Infrastructure.Persistence;

namespace Salonera.Infrastructure.Features;

public sealed class ReportService(IServiceScopeFactory scopeFactory, ICurrentUser currentUser,
    IAuthorizationService authorization) : IReportService
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public async Task<ReportTable> GetAsync(ReportKind kind, ReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(Permissions.FinanceView);
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.To < filter.From) throw new ArgumentException("Bitiş tarihi başlangıç tarihinden önce olamaz.");
        if (filter.To.DayNumber - filter.From.DayNumber > 3660)
            throw new ArgumentException("Rapor aralığı en fazla 10 yıl olabilir.");
        var user = currentUser.User ?? throw new AuthorizationException("Rapor için giriş yapmalısınız.");
        var start = new DateTimeOffset(filter.From.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3));
        var end = new DateTimeOffset(filter.To.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3));
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var report = kind switch
        {
            ReportKind.DailyCash => await DailyCashAsync(db, user.BranchId, filter, end, cancellationToken),
            ReportKind.IncomeExpense => await IncomeExpenseAsync(db, user.BranchId, filter, start, end, cancellationToken),
            ReportKind.EventProfit => await EventProfitAsync(db, user.BranchId, filter, start, end, cancellationToken),
            ReportKind.CustomerBalances => await CustomerBalancesAsync(db, user.BranchId, filter, end, cancellationToken),
            ReportKind.SupplierBalances => await SupplierBalancesAsync(db, user.BranchId, filter, end, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var appliedFilters = new[]
        {
            filter.HallId.HasValue && kind is ReportKind.IncomeExpense or ReportKind.EventProfit ? "Salon" : null,
            filter.CustomerId.HasValue && kind is ReportKind.IncomeExpense or ReportKind.EventProfit or ReportKind.CustomerBalances ? "Müşteri" : null,
            filter.SupplierId.HasValue && kind == ReportKind.SupplierBalances ? "Tedarikçi" : null,
        }.Where(x => x is not null);
        var filterText = string.Join(", ", appliedFilters);
        return report with
        {
            Note = $"Dönem: {filter.From:dd.MM.yyyy} - {filter.To:dd.MM.yyyy}. " +
                (filterText.Length > 0 ? $"Uygulanan filtreler: {filterText}. " : "") + report.Note,
        };
    }

    private static async Task<ReportTable> DailyCashAsync(SaloneraDbContext db, Guid branchId,
        ReportFilter filter, DateTimeOffset end, CancellationToken ct)
    {
        var accounts = await db.CashAccounts.AsNoTracking().Where(x => x.BranchId == branchId)
            .OrderBy(x => x.Name).ToListAsync(ct);
        var movements = await db.Set<FinancialTransaction>().AsNoTracking()
            .Where(x => x.BranchId == branchId && x.AccountType == AccountType.Cash &&
                x.TransactionDate < end && !x.IsCancelled).ToListAsync(ct);
        var rows = new List<ReportRow>();
        foreach (var day in Enumerable.Range(0, filter.To.DayNumber - filter.From.DayNumber + 1)
                     .Select(offset => filter.From.AddDays(offset)))
        {
            var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3));
            var dayEnd = dayStart.AddDays(1);
            foreach (var account in accounts)
            {
                var accountMovements = movements.Where(x => x.AccountId == account.Id).ToArray();
                var opening = account.OpeningBalance + accountMovements
                    .Where(x => x.TransactionDate < dayStart).Sum(x => x.Amount);
                var today = accountMovements.Where(x => x.TransactionDate >= dayStart &&
                    x.TransactionDate < dayEnd).ToArray();
                var incoming = today.Where(x => x.Amount > 0).Sum(x => x.Amount);
                var outgoing = -today.Where(x => x.Amount < 0).Sum(x => x.Amount);
                rows.Add(Row(day.ToString("dd.MM.yyyy", Turkish), account.Name,
                    account.Currency.ToString().ToUpperInvariant(), Money(opening), Money(incoming),
                    Money(outgoing), Money(opening + incoming - outgoing)));
            }
        }
        return new ReportTable("Günlük Kasa", ["Tarih", "Kasa", "Para Birimi", "Açılış",
            "Giriş", "Çıkış", "Kapanış"], rows,
            "Her kasa kendi para biriminde gösterilir; virman ve ters kayıtlar dahildir.");
    }

    private static async Task<ReportTable> IncomeExpenseAsync(SaloneraDbContext db, Guid branchId,
        ReportFilter filter, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var contracts = await db.Contracts.AsNoTracking().Include(x => x.Event)
            .Where(x => x.BranchId == branchId && x.Event.StartsAt >= start && x.Event.StartsAt < end)
            .ToListAsync(ct);
        var incomes = await db.Incomes.AsNoTracking()
            .Where(x => x.BranchId == branchId && !x.IsCancelled && x.IncomeDate >= start && x.IncomeDate < end)
            .ToListAsync(ct);
        var expenses = await db.Expenses.AsNoTracking()
            .Where(x => x.BranchId == branchId && !x.IsCancelled && x.ExpenseDate >= start && x.ExpenseDate < end)
            .ToListAsync(ct);
        var services = await db.EventServices.AsNoTracking().Include(x => x.Event).ThenInclude(x => x.Contract)
            .Where(x => x.Event.BranchId == branchId && x.Event.StartsAt >= start && x.Event.StartsAt < end)
            .ToListAsync(ct);
        if (filter.HallId.HasValue)
        {
            contracts = contracts.Where(x => x.Event.HallId == filter.HallId.Value).ToList();
            incomes = incomes.Where(x => x.EventId.HasValue &&
                contracts.Any(c => c.EventId == x.EventId.Value)).ToList();
            expenses = expenses.Where(x => x.EventId.HasValue &&
                contracts.Any(c => c.EventId == x.EventId.Value)).ToList();
            services = services.Where(x => x.Event.HallId == filter.HallId.Value).ToList();
        }
        if (filter.CustomerId.HasValue)
        {
            contracts = contracts.Where(x => x.Event.CustomerId == filter.CustomerId.Value).ToList();
            incomes = incomes.Where(x => x.CustomerId == filter.CustomerId.Value ||
                x.EventId.HasValue && contracts.Any(c => c.EventId == x.EventId.Value)).ToList();
            expenses = expenses.Where(x => x.EventId.HasValue &&
                contracts.Any(c => c.EventId == x.EventId.Value)).ToList();
            services = services.Where(x => x.Event.CustomerId == filter.CustomerId.Value).ToList();
        }
        var months = Enumerable.Range(0, (filter.To.Year - filter.From.Year) * 12 +
            filter.To.Month - filter.From.Month + 1)
            .Select(offset => new DateOnly(filter.From.Year, filter.From.Month, 1).AddMonths(offset));
        var rows = months.Select(month =>
        {
            var sales = contracts.Where(x => Month(x.Event.StartsAt) == month)
                .Sum(x => x.NetAmountTry);
            var otherIncome = incomes.Where(x => Month(x.IncomeDate) == month).Sum(x => x.AmountTry);
            var expense = expenses.Where(x => Month(x.ExpenseDate) == month).Sum(x => x.AmountTry);
            var serviceCost = services.Where(x => Month(x.Event.StartsAt) == month)
                .Sum(x => decimal.Round(x.Quantity * x.UnitCostSnapshot *
                    (x.Event.Contract?.ExchangeRate ?? 1m), 2));
            return Row(month.ToString("MM.yyyy", Turkish), Money(sales), Money(otherIncome),
                Money(expense), Money(serviceCost), Money(sales + otherIncome - expense - serviceCost));
        }).ToArray();
        return new ReportTable("Gelir, Gider ve Kâr/Zarar", ["Ay", "Sözleşme Geliri (TRY)",
            "Diğer Gelir (TRY)", "Gider (TRY)", "Hizmet Maliyeti (TRY)", "Kâr/Zarar (TRY)"],
            rows, "Tahakkuk esaslıdır: sözleşme geliri ve hizmet maliyeti organizasyon ayında; diğer kayıtlar işlem ayında gösterilir.");
    }

    private static async Task<ReportTable> EventProfitAsync(SaloneraDbContext db, Guid branchId,
        ReportFilter filter, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var events = await db.Events.AsNoTracking().Include(x => x.Customer).Include(x => x.Hall)
            .Include(x => x.Contract)
            .Where(x => x.BranchId == branchId && x.StartsAt >= start && x.StartsAt < end &&
                x.Status != EventStatus.Cancelled && (!filter.HallId.HasValue || x.HallId == filter.HallId) &&
                (!filter.CustomerId.HasValue || x.CustomerId == filter.CustomerId))
            .OrderBy(x => x.StartsAt).ToListAsync(ct);
        var ids = events.Select(x => x.Id).ToArray();
        var services = await db.EventServices.AsNoTracking()
            .Where(x => ids.Contains(x.EventId)).ToListAsync(ct);
        var expenses = await db.Expenses.AsNoTracking()
            .Where(x => x.BranchId == branchId && !x.IsCancelled && x.EventId.HasValue &&
                ids.Contains(x.EventId.Value)).ToListAsync(ct);
        var rows = events.Select(item =>
        {
            var revenue = item.Contract?.NetAmountTry ?? 0m;
            var serviceCost = services.Where(x => x.EventId == item.Id).Sum(x => decimal.Round(x.Quantity *
                x.UnitCostSnapshot * (item.Contract?.ExchangeRate ?? 1m), 2));
            var directCost = expenses.Where(x => x.EventId == item.Id).Sum(x => x.AmountTry);
            return Row(item.EventNumber, item.StartsAt.ToOffset(TimeSpan.FromHours(3))
                .ToString("dd.MM.yyyy", Turkish), item.Customer.FirstName + " " + item.Customer.LastName,
                item.Hall.Name, Money(revenue), Money(serviceCost), Money(directCost),
                Money(revenue - serviceCost - directCost));
        }).ToArray();
        return new ReportTable("Organizasyon Kârlılığı", ["Organizasyon", "Tarih", "Müşteri",
            "Salon", "Gelir (TRY)", "Hizmet Maliyeti (TRY)", "Doğrudan Gider (TRY)", "Kâr (TRY)"],
            rows, "Gelir sözleşme tutarıdır; giderler kayıtlı hizmet maliyeti ve organizasyona bağlı doğrudan giderlerdir.");
    }

    private static async Task<ReportTable> CustomerBalancesAsync(SaloneraDbContext db, Guid branchId,
        ReportFilter filter, DateTimeOffset end, CancellationToken ct)
    {
        var customers = await db.Customers.AsNoTracking()
            .Where(x => x.BranchId == branchId && (!filter.CustomerId.HasValue || x.Id == filter.CustomerId))
            .OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ToListAsync(ct);
        var ids = customers.Select(x => x.Id).ToArray();
        var contracts = await db.Contracts.AsNoTracking().Include(x => x.Event)
            .Where(x => x.BranchId == branchId && ids.Contains(x.Event.CustomerId) && x.ContractDate < end)
            .ToListAsync(ct);
        var contractIds = contracts.Select(x => x.Id).ToArray();
        var receipts = await db.Collections.AsNoTracking()
            .Where(x => x.BranchId == branchId && contractIds.Contains(x.ContractId) &&
                x.CollectedAt < end && !x.IsCancelled).ToListAsync(ct);
        var receiptIds = receipts.Select(x => x.Id).ToArray();
        var refunds = await db.Refunds.AsNoTracking()
            .Where(x => x.BranchId == branchId && contractIds.Contains(x.ContractId) &&
                x.RefundedAt < end && !x.IsCancelled &&
                (!x.CollectionId.HasValue || receiptIds.Contains(x.CollectionId.Value)))
            .ToListAsync(ct);
        var rows = customers.Select(customer =>
        {
            var customerContracts = contracts.Where(x => x.Event.CustomerId == customer.Id).ToArray();
            var customerContractIds = customerContracts.Select(x => x.Id).ToHashSet();
            var total = customerContracts.Sum(x => x.NetAmountTry);
            var paid = receipts.Where(x => customerContractIds.Contains(x.ContractId)).Sum(x => x.AmountTry);
            var returned = refunds.Where(x => customerContractIds.Contains(x.ContractId)).Sum(x => x.AmountTry);
            return Row(customer.FirstName + " " + customer.LastName, customer.Phone,
                Money(total), Money(paid - returned), Money(total - paid + returned));
        }).ToArray();
        return new ReportTable("Müşteri Bakiyeleri", ["Müşteri", "Telefon", "Sözleşme (TRY)",
            "Net Tahsilat (TRY)", "Bakiye (TRY)"], rows,
            "Bitiş tarihi itibarıyla sözleşmeler ve geçerli tahsilatlar; iptal edilen makbuzlar hariçtir.");
    }

    private static async Task<ReportTable> SupplierBalancesAsync(SaloneraDbContext db, Guid branchId,
        ReportFilter filter, DateTimeOffset end, CancellationToken ct)
    {
        var suppliers = await db.Suppliers.AsNoTracking()
            .Where(x => x.BranchId == branchId && (!filter.SupplierId.HasValue || x.Id == filter.SupplierId))
            .OrderBy(x => x.Name).ToListAsync(ct);
        var ids = suppliers.Select(x => x.Id).ToArray();
        var movements = await db.SupplierTransactions.AsNoTracking()
            .Where(x => x.BranchId == branchId && ids.Contains(x.SupplierId) &&
                x.TransactionDate < end && !x.IsCancelled).ToListAsync(ct);
        var rows = suppliers.Select(supplier =>
        {
            var items = movements.Where(x => x.SupplierId == supplier.Id).ToArray();
            var debt = items.Sum(x => decimal.Round(x.Credit * x.ExchangeRate, 2));
            var paid = items.Sum(x => decimal.Round(x.Debit * x.ExchangeRate, 2));
            return Row(supplier.Name, supplier.Phone ?? "", Money(debt), Money(paid),
                Money(debt - paid));
        }).ToArray();
        return new ReportTable("Tedarikçi Bakiyeleri", ["Tedarikçi", "Telefon", "Borç (TRY)",
            "Ödeme (TRY)", "Bakiye (TRY)"], rows,
            "Bitiş tarihi itibarıyla geçerli cari hareketler gösterilir.");
    }

    private static DateOnly Month(DateTimeOffset value)
    {
        var date = value.ToOffset(TimeSpan.FromHours(3));
        return new DateOnly(date.Year, date.Month, 1);
    }

    private static string Money(decimal value) => value.ToString("N2", Turkish);
    private static ReportRow Row(params string[] cells) => new(cells);
}
