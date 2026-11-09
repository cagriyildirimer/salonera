using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Application.Common.Security;
using Salonera.Application.Common.Time;
using Salonera.Domain.Entities;
using Salonera.Infrastructure.Persistence;

namespace Salonera.Infrastructure.Security;

public sealed class AuthenticationService(
    IServiceScopeFactory scopeFactory,
    IPasswordService passwordService,
    IClock clock,
    CurrentUser currentUser) : IAuthenticationService
{
    public async Task<bool> HasAnyUserAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        return await context.Users.IgnoreQueryFilters().AnyAsync(cancellationToken);
    }

    public async Task<LoginResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return new LoginResult(false, "Kullanıcı adı ve parola zorunludur.");
        }

        var normalizedUsername = username.Trim().ToLowerInvariant();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var user = await context.Users
            .Include(item => item.UserRoles)
            .ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(
                item => item.Username == normalizedUsername,
                cancellationToken);

        if (user is null || !user.IsActive)
        {
            context.AuditLogs.Add(CreateLoginAudit(null, false, "LOGIN_REJECTED"));
            await context.SaveChangesAsync(cancellationToken);
            return new LoginResult(false, "Kullanıcı adı veya parola hatalı.");
        }

        var verification = passwordService.Verify(user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            context.AuditLogs.Add(CreateLoginAudit(user, false, "LOGIN_FAILED"));
            await context.SaveChangesAsync(cancellationToken);
            return new LoginResult(false, "Kullanıcı adı veya parola hatalı.");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordService.HashPassword(password);
        }

        user.FailedLoginCount = 0;
        user.LastLoginAt = clock.UtcNow;
        var roles = user.UserRoles.Select(item => item.Role.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var authenticatedUser = new AuthenticatedUser(
            user.Id,
            user.BranchId,
            user.Username,
            user.DisplayName,
            roles,
            RolePermissionProvider.GetPermissions(roles),
            user.MustChangePassword);

        context.AuditLogs.Add(CreateLoginAudit(user, true, "LOGIN_SUCCEEDED"));
        await context.SaveChangesAsync(cancellationToken);
        currentUser.Set(authenticatedUser);

        var message = user.MustChangePassword
            ? "Devam etmek için parolanızı değiştirmeniz gerekiyor."
            : "Giriş başarılı.";
        return new LoginResult(true, message, authenticatedUser);
    }

    public void SignOut() => currentUser.Clear();

    private AuditLog CreateLoginAudit(User? user, bool succeeded, string action) => new()
    {
        BranchId = user?.BranchId,
        UserId = user?.Id,
        Action = action,
        EntityName = nameof(User),
        EntityId = user?.Id.ToString(),
        Succeeded = succeeded,
    };
}
