using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Application.Common.Security;
using Salonera.Application.Common.Time;
using Salonera.Application.Features.Sales;
using Salonera.Domain.Entities;
using Salonera.Domain.Enums;
using Salonera.Infrastructure.Persistence;

namespace Salonera.Infrastructure.Features;

public sealed class SalesContractService(
    IServiceScopeFactory scopeFactory,
    ICurrentUser currentUser,
    IAuthorizationService authorizationService,
    IContractPricingService pricingService,
    IClock clock) : ISalesContractService
{
    public async Task<Guid> CreateContractAsync(
        CreateContractRequest request,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.EventsManage);
        ArgumentNullException.ThrowIfNull(request);
        var user = RequiredUser();
        await using var strategyScope = scopeFactory.CreateAsyncScope();
        var strategyContext = strategyScope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var strategy = strategyContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => CreateContractCoreAsync(request, user, cancellationToken));
    }

    private async Task<Guid> CreateContractCoreAsync(
        CreateContractRequest request,
        AuthenticatedUser user,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        await using var transaction = await BeginTransactionAsync(context, cancellationToken);
        var organizationEvent = await context.Events
            .Include(item => item.Contract)
            .SingleAsync(
                item => item.Id == request.EventId && item.BranchId == user.BranchId,
                cancellationToken);
        if (organizationEvent.Contract is not null)
        {
            throw new InvalidOperationException("Bu organizasyon için zaten sözleşme oluşturulmuş.");
        }

        if (organizationEvent.Status == EventStatus.Cancelled)
        {
            throw new InvalidOperationException("İptal edilmiş organizasyon için sözleşme oluşturulamaz.");
        }

        var package = await context.Packages.SingleAsync(
            item => item.Id == request.PackageId && item.BranchId == user.BranchId && item.IsActive,
            cancellationToken);
        var eventDate = DateOnly.FromDateTime(organizationEvent.StartsAt.Date);
        if ((package.ValidFrom.HasValue && package.ValidFrom > eventDate) ||
            (package.ValidUntil.HasValue && package.ValidUntil < eventDate))
        {
            throw new InvalidOperationException("Paket organizasyon tarihinde geçerli değil.");
        }

        var requestedExtras = request.ExtraServiceIds.Distinct().ToArray();
        var extras = await context.ExtraServices
            .Where(item => requestedExtras.Contains(item.Id) && item.BranchId == user.BranchId && item.IsActive)
            .ToListAsync(cancellationToken);
        if (extras.Count != requestedExtras.Length)
        {
            throw new InvalidOperationException("Seçilen ek hizmetlerden biri bulunamadı veya aktif değil.");
        }

        var lines = new List<PriceLine>
        {
            new(package.Name, 1m, package.SalesPrice, package.CostPrice),
        };
        lines.AddRange(extras.Select(item => new PriceLine(item.Name, 1m, item.SalesPrice, item.CostPrice)));
        var totals = pricingService.Calculate(
            lines,
            request.DiscountAmount,
            request.VatRate,
            request.Currency,
            request.ExchangeRate);

        var packageSnapshot = new EventService
        {
            EventId = organizationEvent.Id,
            PackageId = package.Id,
            DescriptionSnapshot = package.Name,
            Quantity = 1m,
            UnitSalesPriceSnapshot = package.SalesPrice,
            UnitCostSnapshot = package.CostPrice,
        };
        context.EventServices.Add(packageSnapshot);
        foreach (var extra in extras)
        {
            context.EventServices.Add(new EventService
            {
                EventId = organizationEvent.Id,
                ExtraServiceId = extra.Id,
                DescriptionSnapshot = extra.Name,
                Quantity = 1m,
                UnitSalesPriceSnapshot = extra.SalesPrice,
                UnitCostSnapshot = extra.CostPrice,
            });
        }

        var contract = new Contract
        {
            BranchId = user.BranchId,
            EventId = organizationEvent.Id,
            ContractNumber = CreateContractNumber(),
            ContractDate = clock.UtcNow,
            GrossAmount = totals.GrossAmount,
            DiscountAmount = totals.DiscountAmount,
            VatRate = totals.VatRate,
            VatAmount = totals.VatAmount,
            NetAmount = totals.NetAmount,
            Currency = totals.Currency,
            ExchangeRate = totals.ExchangeRate,
            NetAmountTry = totals.NetAmountTry,
        };
        context.Contracts.Add(contract);
        var previousStatus = organizationEvent.Status;
        organizationEvent.Status = EventStatus.Contracted;
        context.EventStatusHistories.Add(new EventStatusHistory
        {
            EventId = organizationEvent.Id,
            PreviousStatus = previousStatus,
            NewStatus = EventStatus.Contracted,
            Reason = "Sözleşme oluşturuldu.",
        });
        context.AuditLogs.Add(Audit(user, contract.Id, "CONTRACT_CREATED"));
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return contract.Id;
    }

    public async Task<ContractDocument> GetContractDocumentAsync(
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.DashboardView);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var contract = await context.Contracts.AsNoTracking()
            .Include(item => item.Branch)
            .Include(item => item.Event).ThenInclude(item => item.Customer)
            .Include(item => item.Event).ThenInclude(item => item.Hall)
            .Include(item => item.Event).ThenInclude(item => item.EventType)
            .SingleAsync(item => item.Id == contractId && item.BranchId == user.BranchId, cancellationToken);
        var services = await context.EventServices.AsNoTracking()
            .Where(item => item.EventId == contract.EventId)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new ContractLineDetail(
                item.DescriptionSnapshot,
                item.Quantity,
                item.UnitSalesPriceSnapshot,
                item.Quantity * item.UnitSalesPriceSnapshot))
            .ToListAsync(cancellationToken);
        var totals = new PriceCalculation(
            contract.GrossAmount,
            contract.DiscountAmount,
            contract.GrossAmount - contract.DiscountAmount,
            contract.VatRate,
            contract.VatAmount,
            contract.NetAmount,
            contract.Currency,
            contract.ExchangeRate,
            contract.NetAmountTry);
        return new ContractDocument(
            contract.Id,
            contract.ContractNumber,
            contract.ContractDate,
            contract.Branch.Name,
            contract.Event.Customer.FirstName + " " + contract.Event.Customer.LastName,
            contract.Event.Customer.Phone,
            contract.Event.Hall.Name,
            contract.Event.EventType.Name,
            contract.Event.StartsAt,
            contract.Event.EndsAt,
            contract.Event.GuestCount,
            services,
            totals);
    }

    public async Task<Guid?> FindContractIdForEventAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.DashboardView);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        return await context.Contracts.AsNoTracking()
            .Where(item => item.EventId == eventId && item.BranchId == user.BranchId)
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static async Task<IDbContextTransaction?> BeginTransactionAsync(
        SaloneraDbContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Database.IsRelational())
        {
            return null;
        }

        return await context.Database.BeginTransactionAsync(cancellationToken);
    }

    private string CreateContractNumber()
        => $"SOZ-{clock.TurkeyNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private AuthenticatedUser RequiredUser() => currentUser.User
        ?? throw new AuthorizationException("Bu işlem için giriş yapmalısınız.");

    private static AuditLog Audit(AuthenticatedUser user, Guid contractId, string action) => new()
    {
        BranchId = user.BranchId,
        UserId = user.Id,
        Action = action,
        EntityName = nameof(Contract),
        EntityId = contractId.ToString(),
        Succeeded = true,
    };
}
