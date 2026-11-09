using Salonera.Application.Common.Security;

namespace Salonera.Infrastructure.Security;

internal static class RolePermissionProvider
{
    private static readonly Dictionary<string, IReadOnlySet<string>> PermissionMap =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [RoleCodes.Administrator] = Permissions.All,
            [RoleCodes.Sales] = Set(
                Permissions.DashboardView,
                Permissions.CustomersManage,
                Permissions.EventsManage,
                Permissions.ReportsView),
            [RoleCodes.Accounting] = Set(
                Permissions.DashboardView,
                Permissions.FinanceView,
                Permissions.FinanceManage,
                Permissions.ReportsView),
            [RoleCodes.Viewer] = Set(
                Permissions.DashboardView,
                Permissions.FinanceView,
                Permissions.ReportsView),
        };

    public static IReadOnlySet<string> GetPermissions(IEnumerable<string> roles)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            if (PermissionMap.TryGetValue(role, out var rolePermissions))
            {
                permissions.UnionWith(rolePermissions);
            }
        }

        return permissions;
    }

    private static HashSet<string> Set(params string[] permissions)
        => new HashSet<string>(permissions, StringComparer.Ordinal);
}
