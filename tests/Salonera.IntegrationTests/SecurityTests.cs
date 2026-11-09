using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Application.Common.Security;
using Salonera.Application.Common.Time;
using Salonera.Domain.Entities;
using Salonera.Infrastructure.Persistence;
using Salonera.Infrastructure.Security;

namespace Salonera.IntegrationTests;

public sealed class SecurityTests
{
    [Fact]
    public void PasswordIsHashedAndVerifiedWithoutStoringPlainText()
    {
        var service = new PasswordService();
        const string password = "Anonim!Test2026";

        var hash = service.HashPassword(password);

        Assert.NotEqual(password, hash);
        Assert.DoesNotContain(password, hash, StringComparison.Ordinal);
        Assert.Equal(PasswordVerificationResult.Success, service.Verify(hash, password));
        Assert.Equal(PasswordVerificationResult.Failed, service.Verify(hash, "Yanlis!Test2026"));
    }

    [Fact]
    public async Task FailedLoginIsAuditedAndSuccessfulLoginLoadsSalesPermissions()
    {
        await using var provider = CreateServiceProvider();
        var passwordService = provider.GetRequiredService<IPasswordService>();
        var identifiers = await SeedUserAsync(provider, passwordService, RoleCodes.Sales, mustChangePassword: false);
        var authentication = provider.GetRequiredService<IAuthenticationService>();

        var failed = await authentication.LoginAsync("test.satis", "Yanlis!Test2026");
        var successful = await authentication.LoginAsync("test.satis", "Anonim!Test2026");

        Assert.False(failed.Succeeded);
        Assert.True(successful.Succeeded);
        Assert.Contains(Permissions.EventsManage, successful.User!.Permissions);
        Assert.DoesNotContain(Permissions.FinanceManage, successful.User.Permissions);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var user = await context.Users.SingleAsync(item => item.Id == identifiers.UserId);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Equal(2, await context.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task UserRequiredToChangePasswordCannotPerformAuthorizedOperation()
    {
        await using var provider = CreateServiceProvider();
        var passwordService = provider.GetRequiredService<IPasswordService>();
        await SeedUserAsync(provider, passwordService, RoleCodes.Administrator, mustChangePassword: true);
        var authentication = provider.GetRequiredService<IAuthenticationService>();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var result = await authentication.LoginAsync("test.satis", "Anonim!Test2026");

        Assert.True(result.Succeeded);
        Assert.True(result.User!.MustChangePassword);
        Assert.False(authorization.HasPermission(Permissions.FinanceManage));
        Assert.Throws<AuthorizationException>(() => authorization.EnsurePermission(Permissions.FinanceManage));
    }

    [Fact]
    public async Task InitialAdministratorCanOnlyBeCreatedOnce()
    {
        await using var provider = CreateServiceProvider();
        await SeedBranchAndRolesAsync(provider);
        var userManagement = provider.GetRequiredService<IUserManagementService>();

        var administrator = await userManagement.CreateInitialAdministratorAsync(
            "admin",
            "Anonim Yönetici",
            "Guvenli!Test2026");

        Assert.Contains(RoleCodes.Administrator, administrator.Roles);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            userManagement.CreateInitialAdministratorAsync("admin2", "İkinci Yönetici", "Guvenli!Test2027"));

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var storedUser = await context.Users.SingleAsync();
        Assert.NotEqual("Guvenli!Test2026", storedUser.PasswordHash);
        Assert.Single(await context.AuditLogs.ToListAsync());
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(new FixedClock(new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero)));
        services.AddSingleton<CurrentUser>();
        services.AddSingleton<ICurrentUser>(provider => provider.GetRequiredService<CurrentUser>());
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<IAuthorizationService, AuthorizationService>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IUserManagementService, UserManagementService>();
        services.AddDbContext<SaloneraDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task<(Guid BranchId, Guid UserId)> SeedUserAsync(
        ServiceProvider provider,
        IPasswordService passwordService,
        string roleCode,
        bool mustChangePassword)
    {
        var (branchId, roles) = await SeedBranchAndRolesAsync(provider);
        var role = roles.Single(item => item.Code == roleCode);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var user = new User
        {
            BranchId = branchId,
            Username = "test.satis",
            DisplayName = "Anonim Satış Kullanıcısı",
            PasswordHash = passwordService.HashPassword("Anonim!Test2026"),
            MustChangePassword = mustChangePassword,
        };
        user.UserRoles.Add(new UserRole { User = user, RoleId = role.Id });
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return (branchId, user.Id);
    }

    private static async Task<(Guid BranchId, IReadOnlyList<Role> Roles)> SeedBranchAndRolesAsync(
        ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var branch = new Branch { Code = "TEST", Name = "Anonim Test Şubesi" };
        var roles = new[]
        {
            new Role { Code = RoleCodes.Administrator, Name = "Yönetici" },
            new Role { Code = RoleCodes.Sales, Name = "Satış" },
            new Role { Code = RoleCodes.Accounting, Name = "Muhasebe" },
            new Role { Code = RoleCodes.Viewer, Name = "Görüntüleme" },
        };
        context.Branches.Add(branch);
        context.Roles.AddRange(roles);
        await context.SaveChangesAsync();
        return (branch.Id, roles);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        public DateTimeOffset TurkeyNow => now.ToOffset(TimeSpan.FromHours(3));
    }
}
