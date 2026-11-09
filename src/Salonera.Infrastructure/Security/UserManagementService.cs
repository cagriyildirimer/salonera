using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Application.Common.Security;
using Salonera.Domain.Entities;
using Salonera.Infrastructure.Persistence;

namespace Salonera.Infrastructure.Security;

public sealed class UserManagementService(
    IServiceScopeFactory scopeFactory,
    IPasswordService passwordService,
    IAuthorizationService authorizationService,
    CurrentUser currentUser) : IUserManagementService
{
    public async Task<IReadOnlyList<UserListItem>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.UsersManage);
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var users = await context.Users
            .AsNoTracking()
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .OrderBy(user => user.DisplayName)
            .ToListAsync(cancellationToken);
        return users
            .Select(user => new UserListItem(
                user.Id,
                user.Username,
                user.DisplayName,
                user.IsActive,
                string.Join(", ", user.UserRoles.Select(userRole => userRole.Role.Name))))
            .ToList();
    }

    public async Task<AuthenticatedUser> CreateInitialAdministratorAsync(
        string username,
        string displayName,
        string password,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        if (await context.Users.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException("İlk yönetici yalnızca kullanıcı bulunmayan sistemde oluşturulabilir.");
        }

        var adminRole = await context.Roles.SingleAsync(
            role => role.Code == RoleCodes.Administrator,
            cancellationToken);
        var branchId = await context.Branches
            .OrderBy(branch => branch.CreatedAt)
            .Select(branch => branch.Id)
            .FirstAsync(cancellationToken);
        var user = CreateUserEntity(branchId, username, displayName, password, mustChangePassword: false);
        user.UserRoles.Add(new UserRole { User = user, RoleId = adminRole.Id });
        context.Users.Add(user);
        context.AuditLogs.Add(CreateUserAudit(user, null, "INITIAL_ADMIN_CREATED"));
        await context.SaveChangesAsync(cancellationToken);

        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { RoleCodes.Administrator };
        return new AuthenticatedUser(
            user.Id,
            user.BranchId,
            user.Username,
            user.DisplayName,
            roles,
            RolePermissionProvider.GetPermissions(roles),
            user.MustChangePassword);
    }

    public async Task<Guid> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        authorizationService.EnsurePermission(Permissions.UsersManage);

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var username = NormalizeUsername(request.Username);
        if (await context.Users.AnyAsync(
                user => user.BranchId == request.BranchId && user.Username == username,
                cancellationToken))
        {
            throw new InvalidOperationException("Bu kullanıcı adı zaten kullanılıyor.");
        }

        var requestedRoleCodes = request.RoleCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roles = await context.Roles
            .Where(role => requestedRoleCodes.Contains(role.Code))
            .ToListAsync(cancellationToken);
        if (roles.Count != requestedRoleCodes.Count || roles.Count == 0)
        {
            throw new InvalidOperationException("Geçersiz veya eksik kullanıcı rolü seçildi.");
        }

        var user = CreateUserEntity(
            request.BranchId,
            username,
            request.DisplayName,
            request.Password,
            request.MustChangePassword);
        foreach (var role in roles)
        {
            user.UserRoles.Add(new UserRole { User = user, RoleId = role.Id });
        }

        context.Users.Add(user);
        context.AuditLogs.Add(CreateUserAudit(user, currentUser.User?.Id, "USER_CREATED"));
        await context.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    public async Task SetUserActiveAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.UsersManage);
        var sessionUser = currentUser.User!;
        if (sessionUser.Id == userId && !isActive)
        {
            throw new InvalidOperationException("Aktif oturumdaki kullanıcı pasif duruma getirilemez.");
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var user = await context.Users.SingleAsync(item => item.Id == userId, cancellationToken);
        user.IsActive = isActive;
        context.AuditLogs.Add(CreateUserAudit(
            user,
            sessionUser.Id,
            isActive ? "USER_ACTIVATED" : "USER_DEACTIVATED"));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var sessionUser = currentUser.User
            ?? throw new AuthorizationException("Parola değiştirmek için giriş yapmalısınız.");
        if (sessionUser.Id != userId)
        {
            authorizationService.EnsurePermission(Permissions.UsersManage);
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var user = await context.Users.SingleAsync(item => item.Id == userId, cancellationToken);
        if (passwordService.Verify(user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            throw new InvalidOperationException("Mevcut parola hatalı.");
        }

        user.PasswordHash = passwordService.HashPassword(newPassword);
        user.MustChangePassword = false;
        context.AuditLogs.Add(CreateUserAudit(user, sessionUser.Id, "PASSWORD_CHANGED"));
        await context.SaveChangesAsync(cancellationToken);

        if (sessionUser.Id == user.Id)
        {
            currentUser.Set(sessionUser with { MustChangePassword = false });
        }
    }

    private User CreateUserEntity(
        Guid branchId,
        string username,
        string displayName,
        string password,
        bool mustChangePassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return new User
        {
            BranchId = branchId,
            Username = NormalizeUsername(username),
            DisplayName = displayName.Trim(),
            PasswordHash = passwordService.HashPassword(password),
            IsActive = true,
            MustChangePassword = mustChangePassword,
        };
    }

    private static string NormalizeUsername(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        return username.Trim().ToLowerInvariant();
    }

    private static AuditLog CreateUserAudit(User user, Guid? performedBy, string action) => new()
    {
        BranchId = user.BranchId,
        UserId = performedBy,
        Action = action,
        EntityName = nameof(User),
        EntityId = user.Id.ToString(),
        Succeeded = true,
    };
}
