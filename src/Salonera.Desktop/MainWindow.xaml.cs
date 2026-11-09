using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Application.Common.Security;
using Salonera.Application.Features.Events;
using Salonera.Desktop.ViewModels;

namespace Salonera.Desktop;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly MainWindowViewModel _viewModel;
    private readonly IAuthenticationService _authentication;
    private readonly ICurrentUser _currentUser;
    private readonly FrameworkElement _dashboard;
    private readonly Stack<NavigationEntry> _history = new();
    private NavigationEntry? _current;

    public MainWindow(MainWindowViewModel viewModel, IServiceProvider services,
        IAuthenticationService authentication, ICurrentUser currentUser)
    {
        _viewModel = viewModel;
        _services = services;
        _authentication = authentication;
        _currentUser = currentUser;
        InitializeComponent();
        DataContext = viewModel;
        _dashboard = (FrameworkElement)PageHost.Content;
    }

    public async Task InitializeAsync()
    {
        if (await _authentication.HasAnyUserAsync()) ShowLogin();
        else ShowInitialSetup();
    }

    private void ShowInitialSetup()
    {
        var view = _services.GetRequiredService<InitialAdministratorWindow>();
        view.Completed += (_, _) => ShowLogin();
        ShowAuthentication(view);
    }

    private void ShowLogin()
    {
        var view = _services.GetRequiredService<LoginWindow>();
        view.Completed += (_, _) =>
        {
            if (_currentUser.User?.MustChangePassword == true) ShowPasswordChange();
            else EnterWorkspace();
        };
        ShowAuthentication(view);
    }

    private void ShowPasswordChange()
    {
        var view = _services.GetRequiredService<ChangePasswordWindow>();
        view.Completed += (_, _) => EnterWorkspace();
        ShowAuthentication(view);
    }

    private void ShowAuthentication(UserControl view)
    {
        Workspace.Visibility = Visibility.Collapsed;
        AuthenticationPanel.Visibility = Visibility.Visible;
        AuthContent.Content = view;
    }

    private void EnterWorkspace()
    {
        _viewModel.RefreshUser();
        AuthContent.Content = null;
        AuthenticationPanel.Visibility = Visibility.Collapsed;
        Workspace.Visibility = Visibility.Visible;
        _history.Clear();
        _current = null;
        ShowPage(new NavigationEntry("home", "Genel Bakış", _dashboard), false);
    }

    private void Navigate(string key, string title, Func<UserControl> create)
    {
        if (_current?.Key == key) return;
        ShowPage(new NavigationEntry(key, title, create()), true);
    }

    private void ShowPage(NavigationEntry entry, bool keepHistory)
    {
        if (keepHistory && _current is not null) _history.Push(_current);
        _current = entry;
        PageHost.Content = entry.Content;
        PageTitle.Text = entry.Title;
        BackButton.IsEnabled = _history.Count > 0;
        foreach (var button in NavItems.Children.OfType<Button>())
        {
            var selected = Equals(button.Tag, entry.Key);
            button.Background = selected ? new SolidColorBrush(Color.FromRgb(237, 235, 255)) : Brushes.Transparent;
            button.Foreground = selected ? new SolidColorBrush(Color.FromRgb(82, 75, 196))
                : new SolidColorBrush(Color.FromRgb(105, 107, 130));
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Medium;
        }
    }

    private void BackClick(object sender, RoutedEventArgs e)
        => GoBack();

    private void GoBack()
    {
        if (_history.Count > 0) ShowPage(_history.Pop(), false);
    }

    private void HomeClick(object sender, RoutedEventArgs e)
    {
        if (_current?.Key != "home")
            ShowPage(new NavigationEntry("home", "Genel Bakış", _dashboard), true);
    }

    private void OpenUsersClick(object sender, RoutedEventArgs e)
        => Navigate("users", "Kullanıcılar", () => _services.GetRequiredService<UserManagementWindow>());

    private void OpenCustomersClick(object sender, RoutedEventArgs e)
        => Navigate("customers", "Müşteriler", () => _services.GetRequiredService<CustomerManagementWindow>());

    private void OpenCatalogsClick(object sender, RoutedEventArgs e)
        => Navigate("catalogs", "Şube ve Salonlar", () => _services.GetRequiredService<CatalogManagementWindow>());

    private void OpenCalendarClick(object sender, RoutedEventArgs e)
    {
        Navigate("calendar", "Takvim ve Organizasyonlar", () =>
        {
            var view = _services.GetRequiredService<CalendarWindow>();
            view.DetailRequested += ShowEventDetail;
            return view;
        });
    }

    private void ShowEventDetail(CalendarEventItem item)
    {
        var view = new EventDetailWindow(item);
        view.BackRequested += (_, _) => GoBack();
        Navigate($"event:{item.Id}", "Organizasyon Detayı", () => view);
    }

    private void OpenSalesClick(object sender, RoutedEventArgs e)
        => Navigate("sales", "Paket ve Sözleşmeler", () => _services.GetRequiredService<SalesContractWindow>());

    private void OpenContractClick(object sender, RoutedEventArgs e)
    {
        if (_current?.Content is SalesContractWindow existing)
        {
            existing.OpenContractTab();
            return;
        }

        Navigate("sales", "Paket ve Sözleşmeler", () =>
        {
            var view = _services.GetRequiredService<SalesContractWindow>();
            view.OpenContractTab();
            return view;
        });
    }

    private void OpenPaymentsClick(object sender, RoutedEventArgs e)
        => Navigate("payments", "Tahsilatlar", () => _services.GetRequiredService<PaymentWindow>());

    private void OpenFinanceClick(object sender, RoutedEventArgs e)
        => Navigate("finance", "Finans ve Cari Hesaplar", () => _services.GetRequiredService<FinanceWindow>());

    private void OpenReportsClick(object sender, RoutedEventArgs e)
        => Navigate("reports", "Ön Muhasebe ve Raporlar", () => _services.GetRequiredService<ReportsWindow>());

    private void SignOutClick(object sender, RoutedEventArgs e)
    {
        _authentication.SignOut();
        _viewModel.RefreshUser();
        _history.Clear();
        _current = null;
        PageHost.Content = null;
        ShowLogin();
    }

    private sealed record NavigationEntry(string Key, string Title, FrameworkElement Content);
}
