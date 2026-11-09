using System.Globalization;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Salonera.Application;
using Salonera.Desktop.ViewModels;
using Salonera.Infrastructure;

namespace Salonera.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            SetTurkishCulture();
            _host = CreateHostBuilder().Build();
            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
            await mainWindow.InitializeAsync();

            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            logger.Log(
                LogLevel.Information,
                new EventId(1000, "ApplicationStarted"),
                "Salonera masaüstü uygulaması başlatıldı.",
                null,
                static (state, _) => state);
        }
        catch (Exception exception)
        {
            _host?.Services
                .GetService<ILogger<App>>()?
                .Log(
                    LogLevel.Critical,
                    new EventId(1002, "ApplicationStartupFailed"),
                    "Salonera başlatılamadı.",
                    exception,
                    static (state, _) => state);

            MessageBox.Show(
                "Salonera başlatılamadı. Lütfen uygulama günlüklerini kontrol edin veya sistem yöneticinize başvurun.",
                "Başlatma Hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.Services
                .GetService<ILogger<App>>()?
                .Log(
                    LogLevel.Information,
                    new EventId(1001, "ApplicationStopping"),
                    "Salonera masaüstü uygulaması kapatılıyor.",
                    null,
                    static (state, _) => state);

            _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(configuration =>
            {
                configuration.SetBasePath(AppContext.BaseDirectory);
                configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddApplication();
                services.AddInfrastructure(context.Configuration);
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
                services.AddTransient<LoginViewModel>();
                services.AddTransient<LoginWindow>();
                services.AddTransient<InitialAdministratorViewModel>();
                services.AddTransient<InitialAdministratorWindow>();
                services.AddTransient<ChangePasswordViewModel>();
                services.AddTransient<ChangePasswordWindow>();
                services.AddTransient<UserManagementViewModel>();
                services.AddTransient<UserManagementWindow>();
                services.AddTransient<CustomerManagementViewModel>();
                services.AddTransient<CustomerManagementWindow>();
                services.AddTransient<CatalogManagementViewModel>();
                services.AddTransient<CatalogManagementWindow>();
                services.AddTransient<CalendarViewModel>();
                services.AddTransient<CalendarWindow>();
                services.AddTransient<SalesContractViewModel>();
                services.AddTransient<SalesContractWindow>();
                services.AddTransient<PaymentViewModel>();
                services.AddTransient<PaymentWindow>();
                services.AddTransient<FinanceViewModel>();
                services.AddTransient<FinanceWindow>();
                services.AddTransient<ReportsViewModel>();
                services.AddTransient<ReportsWindow>();
            });
    }

    private static void SetTurkishCulture()
    {
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
