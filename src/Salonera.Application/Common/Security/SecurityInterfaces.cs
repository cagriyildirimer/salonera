namespace Salonera.Application.Common.Security;

public interface IAuthenticationService
{
    Task<bool> HasAnyUserAsync(CancellationToken cancellationToken = default);

    Task<LoginResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    void SignOut();
}

public interface ICurrentUser
{
    AuthenticatedUser? User { get; }

    bool IsAuthenticated { get; }
}

public interface IAuthorizationService
{
    bool HasPermission(string permission);

    void EnsurePermission(string permission);
}

public interface IUserManagementService
{
    Task<IReadOnlyList<UserListItem>> ListUsersAsync(CancellationToken cancellationToken = default);

    Task<AuthenticatedUser> CreateInitialAdministratorAsync(
        string username,
        string displayName,
        string password,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    Task SetUserActiveAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}

public interface IPasswordService
{
    string HashPassword(string password);

    PasswordVerificationResult Verify(string passwordHash, string suppliedPassword);
}

public enum PasswordVerificationResult
{
    Failed = 0,
    Success = 1,
    SuccessRehashNeeded = 2,
}

public sealed class AuthorizationException(string message) : Exception(message);
