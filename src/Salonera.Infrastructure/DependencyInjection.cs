using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Salonera.Application.Common.Persistence;
using Salonera.Application.Common.Security;
using Salonera.Application.Common.Time;
using Salonera.Application.Features.Catalogs;
using Salonera.Application.Features.Customers;
using Salonera.Application.Features.Events;
using Salonera.Application.Features.Sales;
using Salonera.Application.Features.Payments;
using Salonera.Application.Features.Finance;
using Salonera.Application.Features.Reports;
using Salonera.Infrastructure.Configuration;
using Salonera.Infrastructure.Documents;
using Salonera.Infrastructure.Features;
using Salonera.Infrastructure.Logging;
using Salonera.Infrastructure.Persistence;
using Salonera.Infrastructure.Security;
using Salonera.Infrastructure.Time;

namespace Salonera.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<SaloneraOptions>()
            .Bind(configuration.GetSection(SaloneraOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApplicationName), "Uygulama adı boş olamaz.")
            .Validate(options => options.BaseCurrency is "TRY", "Ana raporlama para birimi TRY olmalıdır.")
            .ValidateOnStart();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<CurrentUser>();
        services.AddSingleton<ICurrentUser>(provider => provider.GetRequiredService<CurrentUser>());
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<IAuthorizationService, AuthorizationService>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IUserManagementService, UserManagementService>();
        services.AddSingleton<ICustomerService, CustomerService>();
        services.AddSingleton<ICatalogService, CatalogService>();
        services.AddSingleton<IEventSchedulingService, EventSchedulingService>();
        services.AddSingleton<IContractPricingService, ContractPricingService>();
        services.AddSingleton<IPackageService, PackageService>();
        services.AddSingleton<ISalesContractService, SalesContractService>();
        services.AddSingleton<IPaymentService, PaymentService>();
        services.AddSingleton<IReceiptPdfService, ReceiptPdfService>();
        services.AddSingleton<IFinanceService, FinanceService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<IReportExportService, ReportExportService>();
        services.AddSingleton<IContractPdfService, ContractPdfService>();

        var connectionString = configuration.GetConnectionString("SaloneraDatabase")
            ?? throw new InvalidOperationException("SaloneraDatabase bağlantı dizesi bulunamadı.");
        services.AddDbContext<SaloneraDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sql =>
                {
                    sql.MigrationsAssembly(typeof(SaloneraDbContext).Assembly.FullName);
                }));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<SaloneraDbContext>());
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddHostedService<DatabaseInitializationService>();

        var configuredLogDirectory = configuration["Logging:File:Directory"] ?? "logs";
        var logDirectory = Path.IsPathRooted(configuredLogDirectory)
            ? configuredLogDirectory
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Salonera",
                configuredLogDirectory);
        var minimumLevelText = configuration["Logging:File:MinimumLevel"];
        var minimumLevel = Enum.TryParse<LogLevel>(minimumLevelText, true, out var parsedLevel)
            ? parsedLevel
            : LogLevel.Information;

        services.AddSingleton<ILoggerProvider>(
            new DailyFileLoggerProvider(new DailyFileLoggerOptions
            {
                DirectoryPath = logDirectory,
                MinimumLevel = minimumLevel,
            }));

        return services;
    }
}
