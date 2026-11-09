using System.Globalization;
using Salonera.Application.Common.Security;
using Salonera.Application.Common.Time;

namespace Salonera.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(IClock clock, ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(clock);

        ApplicationName = "Salonera";
        Subtitle = "Salon Yönetimi ve Ön Muhasebe";
        CurrentUserDisplayName = currentUser.User?.DisplayName ?? "Kullanıcı";
        CanManageUsers = currentUser.User?.Permissions.Contains(Permissions.UsersManage) == true;
        CanManageCustomers = currentUser.User?.Permissions.Contains(Permissions.CustomersManage) == true;
        CanManageEvents = currentUser.User?.Permissions.Contains(Permissions.EventsManage) == true;
        CanManageSettings = currentUser.User?.Permissions.Contains(Permissions.SettingsManage) == true;
        CanViewFinance = currentUser.User?.Permissions.Contains(Permissions.FinanceView) == true;
        StatusText = "Hazır";
        StartupTime = clock.TurkeyNow.ToString(
            "dd MMMM yyyy HH:mm",
            CultureInfo.GetCultureInfo("tr-TR"));
    }

    public string ApplicationName { get; }

    public string Subtitle { get; }

    public string CurrentUserDisplayName { get; }

    public bool CanManageUsers { get; }

    public bool CanManageCustomers { get; }

    public bool CanManageEvents { get; }

    public bool CanManageSettings { get; }

    public bool CanViewFinance { get; }

    public string StatusText { get; }

    public string StartupTime { get; }
}
