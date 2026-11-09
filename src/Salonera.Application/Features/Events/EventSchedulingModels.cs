using Salonera.Domain.Enums;

namespace Salonera.Application.Features.Events;

public sealed record ScheduleEventRequest(
    Guid HallId,
    Guid CustomerId,
    Guid EventTypeId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int GuestCount,
    EventStatus Status,
    DateTimeOffset? OptionExpiresAt = null,
    string? SpecialRequests = null,
    string? InternalNotes = null);

public sealed record CalendarEventItem(
    Guid Id,
    string EventNumber,
    Guid HallId,
    string HallName,
    string CustomerName,
    string EventTypeName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    EventStatus Status,
    DateTimeOffset? OptionExpiresAt);

public interface IEventSchedulingService
{
    Task<IReadOnlyList<CalendarEventItem>> ListAsync(
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        Guid? hallId = null,
        CancellationToken cancellationToken = default);

    Task<Guid> ScheduleAsync(ScheduleEventRequest request, CancellationToken cancellationToken = default);

    Task CancelAsync(Guid eventId, string reason, CancellationToken cancellationToken = default);
}

public sealed class SchedulingConflictException(string message) : Exception(message);
