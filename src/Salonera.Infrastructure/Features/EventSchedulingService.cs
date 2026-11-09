using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Application.Common.Security;
using Salonera.Application.Common.Time;
using Salonera.Application.Features.Events;
using Salonera.Domain.Entities;
using Salonera.Domain.Enums;
using Salonera.Infrastructure.Persistence;

namespace Salonera.Infrastructure.Features;

public sealed class EventSchedulingService(
    IServiceScopeFactory scopeFactory,
    ICurrentUser currentUser,
    IAuthorizationService authorizationService,
    IClock clock) : IEventSchedulingService
{
    public async Task<IReadOnlyList<CalendarEventItem>> ListAsync(
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        Guid? hallId = null,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.DashboardView);
        if (rangeEnd <= rangeStart)
        {
            throw new ArgumentException("Takvim bitiş tarihi başlangıç tarihinden sonra olmalıdır.");
        }

        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var query = context.Events.AsNoTracking()
            .Where(item =>
                item.BranchId == user.BranchId &&
                item.StartsAt < rangeEnd &&
                item.EndsAt > rangeStart);
        if (hallId.HasValue)
        {
            query = query.Where(item => item.HallId == hallId.Value);
        }

        return await query
            .OrderBy(item => item.StartsAt)
            .Select(item => new CalendarEventItem(
                item.Id,
                item.EventNumber,
                item.HallId,
                item.Hall.Name,
                item.Customer.FirstName + " " + item.Customer.LastName,
                item.EventType.Name,
                item.StartsAt,
                item.EndsAt,
                item.Status,
                item.OptionExpiresAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> ScheduleAsync(
        ScheduleEventRequest request,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.EventsManage);
        ValidateRequest(request);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        await using var transaction = await BeginSerializableTransactionAsync(context, cancellationToken);

        if (context.Database.IsRelational())
        {
            var lockResource = $"Salonera:Hall:{request.HallId:N}";
            await context.Database.ExecuteSqlInterpolatedAsync($$"""
                DECLARE @lockResult int;
                EXEC @lockResult = sp_getapplock
                    @Resource={{lockResource}},
                    @LockMode='Exclusive',
                    @LockOwner='Transaction',
                    @LockTimeout=10000;
                IF @lockResult < 0 THROW 51000, 'Salon kilidi alınamadı.', 1;
                """, cancellationToken);
        }

        var hallExists = await context.Halls.AnyAsync(
            hall => hall.Id == request.HallId && hall.BranchId == user.BranchId && hall.IsActive,
            cancellationToken);
        var customerExists = await context.Customers.AnyAsync(
            customer => customer.Id == request.CustomerId && customer.BranchId == user.BranchId,
            cancellationToken);
        var eventTypeExists = await context.EventTypes.AnyAsync(
            eventType => eventType.Id == request.EventTypeId && eventType.BranchId == user.BranchId && eventType.IsActive,
            cancellationToken);
        if (!hallExists || !customerExists || !eventTypeExists)
        {
            throw new InvalidOperationException("Salon, müşteri veya organizasyon türü bulunamadı.");
        }

        var hasConflict = await context.Events.AnyAsync(
            item =>
                item.HallId == request.HallId &&
                item.Status != EventStatus.Cancelled &&
                item.StartsAt < request.EndsAt &&
                item.EndsAt > request.StartsAt,
            cancellationToken);
        if (hasConflict)
        {
            throw new SchedulingConflictException("Seçilen salon bu tarih ve saat aralığında dolu.");
        }

        var organizationEvent = new OrganizationEvent
        {
            BranchId = user.BranchId,
            HallId = request.HallId,
            CustomerId = request.CustomerId,
            EventTypeId = request.EventTypeId,
            EventNumber = CreateEventNumber(),
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            GuestCount = request.GuestCount,
            Status = request.Status,
            OptionExpiresAt = request.OptionExpiresAt,
            SalesRepresentativeId = user.Id,
            SpecialRequests = request.SpecialRequests?.Trim(),
            InternalNotes = request.InternalNotes?.Trim(),
        };
        organizationEvent.StatusHistory.Add(new EventStatusHistory
        {
            Event = organizationEvent,
            PreviousStatus = EventStatus.Draft,
            NewStatus = request.Status,
            Reason = "İlk kayıt",
        });
        context.Events.Add(organizationEvent);
        context.AuditLogs.Add(Audit(user, organizationEvent.Id, "EVENT_SCHEDULED"));
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return organizationEvent.Id;
    }

    public async Task CancelAsync(
        Guid eventId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.EventsManage);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var organizationEvent = await context.Events.SingleAsync(
            item => item.Id == eventId && item.BranchId == user.BranchId,
            cancellationToken);
        var previousStatus = organizationEvent.Status;
        organizationEvent.Status = EventStatus.Cancelled;
        organizationEvent.CancellationDate = clock.UtcNow;
        organizationEvent.CancellationReason = reason.Trim();
        context.EventStatusHistories.Add(new EventStatusHistory
        {
            EventId = organizationEvent.Id,
            PreviousStatus = previousStatus,
            NewStatus = EventStatus.Cancelled,
            Reason = reason.Trim(),
        });
        context.AuditLogs.Add(Audit(user, organizationEvent.Id, "EVENT_CANCELLED"));
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<IDbContextTransaction?> BeginSerializableTransactionAsync(
        SaloneraDbContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Database.IsRelational())
        {
            return null;
        }

        return await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    }

    private void ValidateRequest(ScheduleEventRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EndsAt <= request.StartsAt)
        {
            throw new ArgumentException("Organizasyon bitiş saati başlangıç saatinden sonra olmalıdır.", nameof(request));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.GuestCount);
        if (request.Status == EventStatus.Cancelled)
        {
            throw new ArgumentException("Yeni organizasyon iptal durumunda oluşturulamaz.", nameof(request));
        }

        if (request.Status == EventStatus.Option &&
            (request.OptionExpiresAt is null ||
             request.OptionExpiresAt <= clock.UtcNow ||
             request.OptionExpiresAt >= request.StartsAt))
        {
            throw new ArgumentException(
                "Opsiyon bitiş tarihi gelecekte ve organizasyon başlangıcından önce olmalıdır.",
                nameof(request));
        }
    }

    private string CreateEventNumber()
        => $"ORG-{clock.TurkeyNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private AuthenticatedUser RequiredUser() => currentUser.User
        ?? throw new AuthorizationException("Bu işlem için giriş yapmalısınız.");

    private static AuditLog Audit(AuthenticatedUser user, Guid eventId, string action) => new()
    {
        BranchId = user.BranchId,
        UserId = user.Id,
        Action = action,
        EntityName = nameof(OrganizationEvent),
        EntityId = eventId.ToString(),
        Succeeded = true,
    };
}
