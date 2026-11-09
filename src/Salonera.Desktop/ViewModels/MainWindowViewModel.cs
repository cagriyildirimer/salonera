using System.Globalization;
using Salonera.Application.Common.Security;
using Salonera.Application.Common.Time;

namespace Salonera.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly ICurrentUser _currentUser;

    public MainWindowViewModel(IClock clock, ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _currentUser = currentUser;

        ApplicationName = "Salonera";
        Subtitle = "Salon Yönetimi ve Ön Muhasebe";
        StatusText = "Hazır";
        StartupTime = clock.TurkeyNow.ToString(
            "dd MMMM yyyy HH:mm",
            CultureInfo.GetCultureInfo("tr-TR"));
    }

    public string ApplicationName { get; }

    public string Subtitle { get; }

    public string CurrentUserDisplayName => _currentUser.User?.DisplayName ?? "Kullanıcı";

    public bool CanManageUsers => Has(Permissions.UsersManage);

    public bool CanManageCustomers => Has(Permissions.CustomersManage);

    public bool CanManageEvents => Has(Permissions.EventsManage);

    public bool CanManageSettings => Has(Permissions.SettingsManage);

    public bool CanViewFinance => Has(Permissions.FinanceView);

    public string StatusText { get; }

    public string StartupTime { get; }

    public void RefreshUser()
    {
        OnPropertyChanged(nameof(CurrentUserDisplayName));
        OnPropertyChanged(nameof(CanManageUsers));
        OnPropertyChanged(nameof(CanManageCustomers));
        OnPropertyChanged(nameof(CanManageEvents));
        OnPropertyChanged(nameof(CanManageSettings));
        OnPropertyChanged(nameof(CanViewFinance));
    }

    private bool Has(string permission)
        => _currentUser.User?.Permissions.Contains(permission) == true;
}
