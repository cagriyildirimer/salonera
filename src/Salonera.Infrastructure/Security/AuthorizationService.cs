using Salonera.Application.Common.Security;

namespace Salonera.Infrastructure.Security;

public sealed class AuthorizationService(ICurrentUser currentUser) : IAuthorizationService
{
    public bool HasPermission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return currentUser.User is { MustChangePassword: false } user && user.Permissions.Contains(permission);
    }

    public void EnsurePermission(string permission)
    {
        if (!HasPermission(permission))
        {
            throw new AuthorizationException("Bu işlemi gerçekleştirmek için yetkiniz bulunmuyor.");
        }
    }
}
