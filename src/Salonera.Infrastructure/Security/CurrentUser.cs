using Salonera.Application.Common.Security;

namespace Salonera.Infrastructure.Security;

public sealed class CurrentUser : ICurrentUser
{
    private readonly Lock _lock = new();
    private AuthenticatedUser? _user;

    public AuthenticatedUser? User
    {
        get
        {
            lock (_lock)
            {
                return _user;
            }
        }
    }

    public bool IsAuthenticated => User is not null;

    internal void Set(AuthenticatedUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        lock (_lock)
        {
            _user = user;
        }
    }

    internal void Clear()
    {
        lock (_lock)
        {
            _user = null;
        }
    }
}
