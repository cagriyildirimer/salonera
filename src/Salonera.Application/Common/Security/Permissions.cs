namespace Salonera.Application.Common.Security;

public static class Permissions
{
    public const string DashboardView = "dashboard.view";
    public const string CustomersManage = "customers.manage";
    public const string EventsManage = "events.manage";
    public const string FinanceView = "finance.view";
    public const string FinanceManage = "finance.manage";
    public const string ReportsView = "reports.view";
    public const string SettingsManage = "settings.manage";
    public const string UsersManage = "users.manage";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        DashboardView,
        CustomersManage,
        EventsManage,
        FinanceView,
        FinanceManage,
        ReportsView,
        SettingsManage,
        UsersManage,
    };
}
