using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Salonera.Application;
using Salonera.Application.Common.Time;
using Salonera.Infrastructure;
using Salonera.Infrastructure.Configuration;

namespace Salonera.IntegrationTests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void ApplicationAndInfrastructureServicesCanBeResolved()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:SaloneraDatabase"] = "Server=localhost;Database=SaloneraTest;TrustServerCertificate=True",
            ["Salonera:ApplicationName"] = "Salonera",
            ["Salonera:BaseCurrency"] = "TRY",
            ["Logging:File:Directory"] = Path.Combine(Path.GetTempPath(), "salonera-tests"),
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var clock = provider.GetRequiredService<IClock>();
        var options = provider.GetRequiredService<IOptions<SaloneraOptions>>().Value;

        Assert.Equal(TimeSpan.FromHours(3), clock.TurkeyNow.Offset);
        Assert.Equal("Salonera", options.ApplicationName);
        Assert.Equal("TRY", options.BaseCurrency);
        Assert.Same(clock, provider.GetRequiredService<IClock>());
        Assert.NotEmpty(provider.GetServices<ILoggerProvider>());
    }
}
