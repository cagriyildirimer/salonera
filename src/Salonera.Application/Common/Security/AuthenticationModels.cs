namespace Salonera.Application.Common.Security;

public sealed record AuthenticatedUser(
    Guid Id,
    Guid BranchId,
    string Username,
    string DisplayName,
    IReadOnlySet<string> Roles,
    IReadOnlySet<string> Permissions,
    bool MustChangePassword);

public sealed record LoginResult(
    bool Succeeded,
    string UserMessage,
    AuthenticatedUser? User = null);

public sealed record CreateUserRequest(
    Guid BranchId,
    string Username,
    string DisplayName,
    string Password,
    IReadOnlyCollection<string> RoleCodes,
    bool MustChangePassword = true);

public sealed record UserListItem(
    Guid Id,
    string Username,
    string DisplayName,
    bool IsActive,
    string Roles);
