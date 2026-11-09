using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Application.Common.Security;
using Salonera.Application.Common.Time;
using Salonera.Application.Features.Sales;
using Salonera.Domain.Entities;
using Salonera.Domain.Enums;
using Salonera.Infrastructure.Documents;
using Salonera.Infrastructure.Features;
using Salonera.Infrastructure.Persistence;
using Salonera.Infrastructure.Security;

namespace Salonera.IntegrationTests;

public sealed class SalesContractTests
{
    [Fact]
    public void DiscountVatAndExchangeRateAreCalculatedWithDecimalPrecision()
    {
        var service = new ContractPricingService();

        var result = service.Calculate(
            [new PriceLine("Paket", 1m, 1000m, 600m), new PriceLine("Fotoğraf", 1m, 200m, 100m)],
            100m,
            20m,
            CurrencyCode.Eur,
            40m);

        Assert.Equal(1200m, result.GrossAmount);
        Assert.Equal(1100m, result.TaxableAmount);
        Assert.Equal(220m, result.VatAmount);
        Assert.Equal(1320m, result.NetAmount);
        Assert.Equal(52800m, result.NetAmountTry);
    }

    [Fact]
    public async Task PackagePricesAreSnapshottedWhenContractIsCreated()
    {
        var setup = await CreateSetupAsync();
        await using var provider = setup.Provider;
        var service = provider.GetRequiredService<ISalesContractService>();
        var contractId = await service.CreateContractAsync(new CreateContractRequest(
            setup.EventId,
            setup.PackageId,
            [setup.ExtraServiceId],
            100m,
            20m,
            CurrencyCode.Try,
            1m));

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
            var package = await context.Packages.SingleAsync(item => item.Id == setup.PackageId);
            package.SalesPrice = 9999m;
            await context.SaveChangesAsync();
        }

        var document = await service.GetContractDocumentAsync(contractId);

        Assert.Equal(1000m, document.Lines.Single(line => line.Description == "Düğün Paketi").UnitPrice);
        Assert.Equal(1320m, document.Totals.NetAmount);
        await using var verificationScope = provider.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var organizationEvent = await verificationContext.Events.SingleAsync(item => item.Id == setup.EventId);
        Assert.Equal(EventStatus.Contracted, organizationEvent.Status);
        Assert.Contains(
            await verificationContext.EventStatusHistories.ToListAsync(),
            item => item.EventId == setup.EventId && item.NewStatus == EventStatus.Contracted);
    }

    [Fact]
    public async Task ContractPdfRendersAsValidPdfFile()
    {
        var requestedOutput = Environment.GetEnvironmentVariable("SALONERA_PDF_OUTPUT");
        var outputPath = requestedOutput ?? Path.Combine(
            Path.GetTempPath(),
            "salonera-tests",
            $"contract-{Guid.NewGuid():N}.pdf");
        try
        {
            var renderer = new ContractPdfService(new UnusedSalesContractService());
            await renderer.RenderAsync(SampleDocument(), outputPath);

            var bytes = await File.ReadAllBytesAsync(outputPath);
            Assert.True(bytes.Length > 5000);
            Assert.Equal("%PDF-"u8.ToArray(), bytes[..5]);
        }
        finally
        {
            if (requestedOutput is null && File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static ContractDocument SampleDocument()
    {
        var lines = new[]
        {
            new ContractLineDetail("Salon ve yemek paketi", 1m, 85000m, 85000m),
            new ContractLineDetail("Fotoğraf ve video", 1m, 12000m, 12000m),
            new ContractLineDetail("Özel masa süslemesi", 1m, 8000m, 8000m),
        };
        var totals = new PriceCalculation(
            105000m,
            5000m,
            100000m,
            20m,
            20000m,
            120000m,
            CurrencyCode.Try,
            1m,
            120000m);
        return new ContractDocument(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "SOZ-20261109-ORNEK1",
            new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.FromHours(3)),
            "Merkez Şube",
            "Deniz Yılmaz",
            "0500 000 00 00",
            "Büyük Salon",
            "Düğün",
            new DateTimeOffset(2027, 6, 12, 18, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2027, 6, 12, 23, 0, 0, TimeSpan.FromHours(3)),
            350,
            lines,
            totals);
    }

    private static async Task<SalesSetup> CreateSetupAsync()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(new FixedClock(new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero)));
        services.AddSingleton<CurrentUser>();
        services.AddSingleton<ICurrentUser>(provider => provider.GetRequiredService<CurrentUser>());
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<IAuthorizationService, AuthorizationService>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IContractPricingService, ContractPricingService>();
        services.AddSingleton<ISalesContractService, SalesContractService>();
        services.AddDbContext<SaloneraDbContext>(options => options.UseInMemoryDatabase(databaseName));
        var provider = services.BuildServiceProvider(validateScopes: true);

        var passwordService = provider.GetRequiredService<IPasswordService>();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var branch = new Branch { Code = "TEST", Name = "Anonim Test Şubesi" };
        var hall = new Hall { Branch = branch, Code = "A", Name = "Salon A", Capacity = 500 };
        var customer = new Customer { Branch = branch, FirstName = "Anonim", LastName = "Müşteri", Phone = "05000000000" };
        var eventType = new EventType { Branch = branch, Name = "Düğün" };
        var package = new Package
        {
            Branch = branch,
            Name = "Düğün Paketi",
            IncludedGuestCount = 300,
            SalesPrice = 1000m,
            CostPrice = 600m,
        };
        var extra = new ExtraService { Branch = branch, Name = "Fotoğraf", SalesPrice = 200m, CostPrice = 100m };
        var role = new Role { Code = RoleCodes.Administrator, Name = "Yönetici" };
        var user = new User
        {
            Branch = branch,
            Username = "admin",
            DisplayName = "Test Yöneticisi",
            PasswordHash = passwordService.HashPassword("Anonim!Test2026"),
            MustChangePassword = false,
        };
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        var organizationEvent = new OrganizationEvent
        {
            Branch = branch,
            Hall = hall,
            Customer = customer,
            EventType = eventType,
            EventNumber = "ORG-TEST-1",
            StartsAt = new DateTimeOffset(2027, 6, 12, 18, 0, 0, TimeSpan.FromHours(3)),
            EndsAt = new DateTimeOffset(2027, 6, 12, 23, 0, 0, TimeSpan.FromHours(3)),
            GuestCount = 300,
            Status = EventStatus.Option,
        };
        context.AddRange(branch, hall, customer, eventType, package, extra, role, user, organizationEvent);
        await context.SaveChangesAsync();

        var login = await provider.GetRequiredService<IAuthenticationService>().LoginAsync("admin", "Anonim!Test2026");
        Assert.True(login.Succeeded);
        return new SalesSetup(provider, organizationEvent.Id, package.Id, extra.Id);
    }

    private sealed record SalesSetup(ServiceProvider Provider, Guid EventId, Guid PackageId, Guid ExtraServiceId);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
        public DateTimeOffset TurkeyNow => now.ToOffset(TimeSpan.FromHours(3));
    }

    private sealed class UnusedSalesContractService : ISalesContractService
    {
        public Task<Guid> CreateContractAsync(CreateContractRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ContractDocument> GetContractDocumentAsync(Guid contractId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
