using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Application.Common.Security;
using Salonera.Application.Common.Time;
using Salonera.Application.Features.Customers;
using Salonera.Application.Features.Events;
using Salonera.Domain.Entities;
using Salonera.Domain.Enums;
using Salonera.Infrastructure.Features;
using Salonera.Infrastructure.Persistence;
using Salonera.Infrastructure.Security;

namespace Salonera.IntegrationTests;

public sealed class SchedulingAndCustomerTests
{
    [Fact]
    public async Task SameHallAndSameIntervalIsRejected()
    {
        var setup = await CreateAuthenticatedSetupAsync();
        await using var provider = setup.Provider;
        var service = provider.GetRequiredService<IEventSchedulingService>();
        var request = Request(setup, Hour(18), Hour(22));

        await service.ScheduleAsync(request);

        await Assert.ThrowsAsync<SchedulingConflictException>(() => service.ScheduleAsync(request));
    }

    [Fact]
    public async Task SameHallAndOverlappingTimeIsRejected()
    {
        var setup = await CreateAuthenticatedSetupAsync();
        await using var provider = setup.Provider;
        var service = provider.GetRequiredService<IEventSchedulingService>();
        var first = Request(setup, Hour(18), Hour(22));
        var overlapping = Request(setup, Hour(19), Hour(21));

        await service.ScheduleAsync(first);

        await Assert.ThrowsAsync<SchedulingConflictException>(() => service.ScheduleAsync(overlapping));
    }

    [Fact]
    public async Task AdjacentEventDoesNotConflict()
    {
        var setup = await CreateAuthenticatedSetupAsync();
        await using var provider = setup.Provider;
        var service = provider.GetRequiredService<IEventSchedulingService>();

        await service.ScheduleAsync(Request(setup, Hour(18), Hour(22)));
        var adjacentId = await service.ScheduleAsync(Request(setup, Hour(22), Hour(23)));

        Assert.NotEqual(Guid.Empty, adjacentId);
    }

    [Fact]
    public async Task CancelledEventReleasesHallTime()
    {
        var setup = await CreateAuthenticatedSetupAsync();
        await using var provider = setup.Provider;
        var service = provider.GetRequiredService<IEventSchedulingService>();
        var request = Request(setup, Hour(18), Hour(22));
        var eventId = await service.ScheduleAsync(request);

        await service.CancelAsync(eventId, "Anonim test iptali");
        var replacementId = await service.ScheduleAsync(request);

        Assert.NotEqual(eventId, replacementId);
    }

    [Fact]
    public async Task OptionRequiresValidExpirationDate()
    {
        var setup = await CreateAuthenticatedSetupAsync();
        await using var provider = setup.Provider;
        var service = provider.GetRequiredService<IEventSchedulingService>();
        var invalid = Request(setup, Hour(18), Hour(22)) with
        {
            Status = EventStatus.Option,
            OptionExpiresAt = Hour(19),
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.ScheduleAsync(invalid));
    }

    [Fact]
    public async Task CustomerCrudUsesBranchFilterAndSoftDelete()
    {
        var setup = await CreateAuthenticatedSetupAsync();
        await using var provider = setup.Provider;
        var service = provider.GetRequiredService<ICustomerService>();
        var request = new SaveCustomerRequest(
            "Deniz",
            "Yılmaz",
            "0500 000 00 00",
            Email: "deniz@example.test",
            BrideName: "Deniz",
            GroomName: "Ege");
        var id = await service.CreateAsync(request);
        var detail = await service.GetAsync(id);

        await service.UpdateAsync(id, request with { LastName = "Kaya" }, detail.RowVersion);
        var updated = await service.GetAsync(id);
        await service.DeleteAsync(id);

        Assert.Equal("Kaya", updated.LastName);
        Assert.DoesNotContain(await service.ListAsync(), item => item.Id == id);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        Assert.Single(await context.Customers.IgnoreQueryFilters().Where(item => item.Id == id).ToListAsync());
    }

    private static ScheduleEventRequest Request(TestSetup setup, DateTimeOffset startsAt, DateTimeOffset endsAt)
        => new(
            setup.HallId,
            setup.CustomerId,
            setup.EventTypeId,
            startsAt,
            endsAt,
            250,
            EventStatus.Contracted);

    private static DateTimeOffset Hour(int hour)
        => new(2026, 12, 20, hour, 0, 0, TimeSpan.FromHours(3));

    private static async Task<TestSetup> CreateAuthenticatedSetupAsync()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(new FixedClock(new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero)));
        services.AddSingleton<CurrentUser>();
        services.AddSingleton<ICurrentUser>(provider => provider.GetRequiredService<CurrentUser>());
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<IAuthorizationService, AuthorizationService>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IEventSchedulingService, EventSchedulingService>();
        services.AddSingleton<ICustomerService, CustomerService>();
        services.AddDbContext<SaloneraDbContext>(options => options.UseInMemoryDatabase(databaseName));
        var provider = services.BuildServiceProvider(validateScopes: true);

        var passwordService = provider.GetRequiredService<IPasswordService>();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var branch = new Branch { Code = "TEST", Name = "Anonim Test Şubesi" };
        var hall = new Hall { Branch = branch, Code = "SALON-A", Name = "Salon A", Capacity = 500 };
        var customer = new Customer
        {
            Branch = branch,
            FirstName = "Anonim",
            LastName = "Müşteri",
            Phone = "05000000000",
        };
        var eventType = new EventType { Branch = branch, Name = "Düğün" };
        var role = new Role { Code = RoleCodes.Administrator, Name = "Yönetici" };
        var user = new User
        {
            Branch = branch,
            Username = "admin",
            DisplayName = "Test Yöneticisi",
            PasswordHash = passwordService.HashPassword("Anonim!Test2026"),
            MustChangePassword = false,
        };
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        context.AddRange(branch, hall, customer, eventType, role, user);
        await context.SaveChangesAsync();

        var authentication = provider.GetRequiredService<IAuthenticationService>();
        var login = await authentication.LoginAsync("admin", "Anonim!Test2026");
        Assert.True(login.Succeeded);
        return new TestSetup(provider, hall.Id, customer.Id, eventType.Id);
    }

    private sealed record TestSetup(
        ServiceProvider Provider,
        Guid HallId,
        Guid CustomerId,
        Guid EventTypeId);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        public DateTimeOffset TurkeyNow => now.ToOffset(TimeSpan.FromHours(3));
    }
}
