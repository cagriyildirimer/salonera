using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Application.Common.Security;
using Salonera.Application.Common.Time;
using Salonera.Application.Features.Catalogs;
using Salonera.Domain.Entities;
using Salonera.Domain.Enums;
using Salonera.Infrastructure.Persistence;

namespace Salonera.Infrastructure.Features;

public sealed class CatalogService(
    IServiceScopeFactory scopeFactory,
    ICurrentUser currentUser,
    IAuthorizationService authorizationService,
    IClock clock) : ICatalogService
{
    public async Task<IReadOnlyList<BranchListItem>> ListBranchesAsync(
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.DashboardView);
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        return await context.Branches.AsNoTracking()
            .OrderBy(branch => branch.Name)
            .Select(branch => new BranchListItem(branch.Id, branch.Code, branch.Name, branch.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HallListItem>> ListHallsAsync(
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.DashboardView);
        var selectedBranchId = branchId ?? RequiredUser().BranchId;
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        return await context.Halls.AsNoTracking()
            .Where(hall => hall.BranchId == selectedBranchId)
            .OrderBy(hall => hall.Name)
            .Select(hall => new HallListItem(hall.Id, hall.BranchId, hall.Code, hall.Name, hall.Capacity, hall.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EventTypeListItem>> ListEventTypesAsync(
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.DashboardView);
        var branchId = RequiredUser().BranchId;
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        return await context.EventTypes.AsNoTracking()
            .Where(eventType => eventType.BranchId == branchId)
            .OrderBy(eventType => eventType.Name)
            .Select(eventType => new EventTypeListItem(eventType.Id, eventType.Name, eventType.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> CreateBranchAsync(
        string code,
        string name,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.SettingsManage);
        ValidateCodeAndName(code, name);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var branch = new Branch { Code = code.Trim().ToUpperInvariant(), Name = name.Trim() };
        context.Branches.Add(branch);
        context.AuditLogs.Add(Audit(user, nameof(Branch), branch.Id, "BRANCH_CREATED"));
        await context.SaveChangesAsync(cancellationToken);
        return branch.Id;
    }

    public async Task<Guid> CreateHallAsync(
        Guid branchId,
        string code,
        string name,
        int capacity,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.SettingsManage);
        ValidateCodeAndName(code, name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        _ = await context.Branches.SingleAsync(branch => branch.Id == branchId, cancellationToken);
        var hall = new Hall
        {
            BranchId = branchId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Capacity = capacity,
        };
        context.Halls.Add(hall);
        context.AuditLogs.Add(Audit(user, nameof(Hall), hall.Id, "HALL_CREATED"));
        await context.SaveChangesAsync(cancellationToken);
        return hall.Id;
    }

    public async Task UpdateHallAsync(
        Guid id,
        string code,
        string name,
        int capacity,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.SettingsManage);
        ValidateCodeAndName(code, name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var hall = await context.Halls.SingleAsync(item => item.Id == id, cancellationToken);
        hall.Code = code.Trim().ToUpperInvariant();
        hall.Name = name.Trim();
        hall.Capacity = capacity;
        hall.IsActive = isActive;
        context.AuditLogs.Add(Audit(user, nameof(Hall), hall.Id, "HALL_UPDATED"));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteHallAsync(Guid id, CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.SettingsManage);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var hall = await context.Halls.SingleAsync(item => item.Id == id, cancellationToken);
        var hasUpcomingEvent = await context.Events.AnyAsync(
            item => item.HallId == id && item.StartsAt >= clock.UtcNow && item.Status != EventStatus.Cancelled,
            cancellationToken);
        if (hasUpcomingEvent)
        {
            throw new InvalidOperationException("Yaklaşan organizasyonu bulunan salon silinemez.");
        }

        context.Halls.Remove(hall);
        context.AuditLogs.Add(Audit(user, nameof(Hall), hall.Id, "HALL_DELETED"));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> CreateEventTypeAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.SettingsManage);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var eventType = new EventType { BranchId = user.BranchId, Name = name.Trim() };
        context.EventTypes.Add(eventType);
        context.AuditLogs.Add(Audit(user, nameof(EventType), eventType.Id, "EVENT_TYPE_CREATED"));
        await context.SaveChangesAsync(cancellationToken);
        return eventType.Id;
    }

    private static void ValidateCodeAndName(string code, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
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
