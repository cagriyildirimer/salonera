using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Salonera.Application.Common.Time;
using Salonera.Application.Features.Catalogs;
using Salonera.Application.Features.Customers;
using Salonera.Application.Features.Events;
using Salonera.Domain.Enums;

namespace Salonera.Desktop.ViewModels;

public sealed class CalendarViewModel(
    IEventSchedulingService schedulingService,
    ICatalogService catalogService,
    ICustomerService customerService,
    IClock clock,
    ILogger<CalendarViewModel> logger) : ObservableObject
{
    private HallListItem? _selectedHall;
    private CustomerListItem? _selectedCustomer;
    private EventTypeListItem? _selectedEventType;
    private CalendarEventItem? _selectedEvent;
    private DateTime? _eventDate = clock.TurkeyNow.Date.AddDays(1);
    private string _startTime = "18:00";
    private string _endTime = "22:00";
    private string _guestCount = "100";
    private EventStatus _status = EventStatus.Option;
    private DateTime? _calendarDate = clock.TurkeyNow.Date;
    private CalendarRangeMode _selectedRangeMode = CalendarRangeMode.Aylık;
    private string _message = string.Empty;

    public ObservableCollection<CalendarEventItem> Events { get; } = [];
    public ObservableCollection<HallListItem> Halls { get; } = [];
    public ObservableCollection<CustomerListItem> Customers { get; } = [];
    public ObservableCollection<EventTypeListItem> EventTypes { get; } = [];
    public IReadOnlyList<EventStatus> Statuses { get; } = [EventStatus.Draft, EventStatus.Option, EventStatus.Contracted];
    public IReadOnlyList<CalendarRangeMode> RangeModes { get; } = Enum.GetValues<CalendarRangeMode>();
    public HallListItem? SelectedHall { get => _selectedHall; set => SetProperty(ref _selectedHall, value); }
    public CustomerListItem? SelectedCustomer { get => _selectedCustomer; set => SetProperty(ref _selectedCustomer, value); }
    public EventTypeListItem? SelectedEventType { get => _selectedEventType; set => SetProperty(ref _selectedEventType, value); }
    public CalendarEventItem? SelectedEvent { get => _selectedEvent; set => SetProperty(ref _selectedEvent, value); }
    public DateTime? EventDate { get => _eventDate; set => SetProperty(ref _eventDate, value); }
    public string StartTime { get => _startTime; set => SetProperty(ref _startTime, value); }
    public string EndTime { get => _endTime; set => SetProperty(ref _endTime, value); }
    public string GuestCount { get => _guestCount; set => SetProperty(ref _guestCount, value); }
    public EventStatus Status { get => _status; set => SetProperty(ref _status, value); }
    public DateTime? CalendarDate { get => _calendarDate; set => SetProperty(ref _calendarDate, value); }
    public CalendarRangeMode SelectedRangeMode { get => _selectedRangeMode; set => SetProperty(ref _selectedRangeMode, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    public async Task LoadAsync()
    {
        Replace(Halls, await catalogService.ListHallsAsync());
        Replace(Customers, await customerService.ListAsync());
        Replace(EventTypes, await catalogService.ListEventTypesAsync());
        SelectedHall ??= Halls.FirstOrDefault();
        SelectedCustomer ??= Customers.FirstOrDefault();
        SelectedEventType ??= EventTypes.FirstOrDefault();
        await LoadCalendarAsync();
    }

    public async Task LoadCalendarAsync()
    {
        var selectedDate = CalendarDate ?? clock.TurkeyNow.Date;
        var date = DateOnly.FromDateTime(selectedDate);
        DateOnly rangeStart;
        DateOnly rangeEnd;
        switch (SelectedRangeMode)
        {
            case CalendarRangeMode.Günlük:
                rangeStart = date;
                rangeEnd = date.AddDays(1);
                break;
            case CalendarRangeMode.Haftalık:
                var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
                rangeStart = date.AddDays(-daysSinceMonday);
                rangeEnd = rangeStart.AddDays(7);
                break;
            default:
                rangeStart = new DateOnly(date.Year, date.Month, 1);
                rangeEnd = rangeStart.AddMonths(1);
                break;
        }

        var start = new DateTimeOffset(rangeStart.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3));
        var end = new DateTimeOffset(rangeEnd.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3));
        var events = await schedulingService.ListAsync(start, end, SelectedHall?.Id);
        Replace(Events, events);
    }

    public async Task ScheduleAsync()
    {
        if (SelectedHall is null || SelectedCustomer is null || SelectedEventType is null || EventDate is null ||
            !TimeOnly.TryParse(StartTime, CultureInfo.GetCultureInfo("tr-TR"), out var startTime) ||
            !TimeOnly.TryParse(EndTime, CultureInfo.GetCultureInfo("tr-TR"), out var endTime) ||
            !int.TryParse(GuestCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var guestCount))
        {
            Message = "Salon, müşteri, tür, tarih, saat ve davetli sayısını kontrol edin.";
            return;
        }

        var date = DateOnly.FromDateTime(EventDate.Value);
        var startsAt = new DateTimeOffset(date.ToDateTime(startTime), TimeSpan.FromHours(3));
        var endsAt = new DateTimeOffset(date.ToDateTime(endTime), TimeSpan.FromHours(3));
        var optionExpiresAt = startsAt.AddDays(-2);
        if (Status == EventStatus.Option && optionExpiresAt <= clock.UtcNow && startsAt > clock.UtcNow)
        {
            optionExpiresAt = clock.UtcNow.AddTicks((startsAt - clock.UtcNow).Ticks / 2);
        }
        try
        {
            await schedulingService.ScheduleAsync(new ScheduleEventRequest(
                SelectedHall.Id,
                SelectedCustomer.Id,
                SelectedEventType.Id,
                startsAt,
                endsAt,
                guestCount,
                Status,
                Status == EventStatus.Option ? optionExpiresAt : null));
            Message = "Organizasyon takvime eklendi.";
            await LoadCalendarAsync();
        }
        catch (SchedulingConflictException exception)
        {
            Message = exception.Message;
        }
        catch (ArgumentException exception)
        {
            Message = exception.Message;
        }
        catch (Exception exception)
        {
            logger.Log(
                LogLevel.Error,
                new EventId(3001, "EventSchedulingFailed"),
                "Organizasyon takvime eklenemedi.",
                exception,
                static (state, _) => state);
            Message = "Organizasyon eklenemedi. Ayrıntılar uygulama günlüğüne kaydedildi.";
        }
    }

    public async Task CancelSelectedAsync()
    {
        if (SelectedEvent is null)
        {
            Message = "Önce bir organizasyon seçin.";
            return;
        }

        await schedulingService.CancelAsync(SelectedEvent.Id, "Kullanıcı tarafından takvimden iptal edildi.");
        Message = "Organizasyon iptal edildi.";
        await LoadCalendarAsync();
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }

    public enum CalendarRangeMode
    {
        Günlük,
        Haftalık,
        Aylık,
    }
}
