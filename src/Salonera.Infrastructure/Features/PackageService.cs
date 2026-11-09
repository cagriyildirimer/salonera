using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Application.Common.Security;
using Salonera.Application.Features.Sales;
using Salonera.Domain.Entities;
using Salonera.Infrastructure.Persistence;

namespace Salonera.Infrastructure.Features;

public sealed class PackageService(
    IServiceScopeFactory scopeFactory,
    ICurrentUser currentUser,
    IAuthorizationService authorizationService) : IPackageService
{
    public async Task<IReadOnlyList<PackageListItem>> ListPackagesAsync(
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.DashboardView);
        var branchId = RequiredUser().BranchId;
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        return await context.Packages.AsNoTracking()
            .Where(item => item.BranchId == branchId)
            .OrderBy(item => item.Name)
            .Select(item => new PackageListItem(
                item.Id,
                item.Name,
                item.IncludedGuestCount,
                item.SalesPrice,
                item.CostPrice,
                item.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExtraServiceListItem>> ListExtraServicesAsync(
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.DashboardView);
        var branchId = RequiredUser().BranchId;
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        return await context.ExtraServices.AsNoTracking()
            .Where(item => item.BranchId == branchId)
            .OrderBy(item => item.Name)
            .Select(item => new ExtraServiceListItem(item.Id, item.Name, item.SalesPrice, item.CostPrice, item.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> CreatePackageAsync(
        SavePackageRequest request,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.SettingsManage);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.IncludedGuestCount);
        ArgumentOutOfRangeException.ThrowIfNegative(request.SalesPrice);
        ArgumentOutOfRangeException.ThrowIfNegative(request.CostPrice);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var package = new Package
        {
            BranchId = user.BranchId,
            Name = request.Name.Trim(),
            IncludedGuestCount = request.IncludedGuestCount,
            SalesPrice = request.SalesPrice,
            CostPrice = request.CostPrice,
            PricePerPerson = request.PricePerPerson,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
        };
        context.Packages.Add(package);
        context.AuditLogs.Add(Audit(user, nameof(Package), package.Id, "PACKAGE_CREATED"));
        await context.SaveChangesAsync(cancellationToken);
        return package.Id;
    }

    public async Task<Guid> CreateExtraServiceAsync(
        SaveExtraServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.SettingsManage);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentOutOfRangeException.ThrowIfNegative(request.SalesPrice);
        ArgumentOutOfRangeException.ThrowIfNegative(request.CostPrice);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var extraService = new ExtraService
        {
            BranchId = user.BranchId,
            Name = request.Name.Trim(),
            SalesPrice = request.SalesPrice,
            CostPrice = request.CostPrice,
        };
        context.ExtraServices.Add(extraService);
        context.AuditLogs.Add(Audit(user, nameof(ExtraService), extraService.Id, "EXTRA_SERVICE_CREATED"));
        await context.SaveChangesAsync(cancellationToken);
        return extraService.Id;
    }

    private AuthenticatedUser RequiredUser() => currentUser.User
        ?? throw new AuthorizationException("Bu işlem için giriş yapmalısınız.");

    private static AuditLog Audit(AuthenticatedUser user, string entityName, Guid entityId, string action) => new()
    {
        BranchId = user.BranchId,
        UserId = user.Id,
        Action = action,
        EntityName = entityName,
        EntityId = entityId.ToString(),
        Succeeded = true,
    };
}
