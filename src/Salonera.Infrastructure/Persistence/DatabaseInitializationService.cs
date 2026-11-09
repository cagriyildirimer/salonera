using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Salonera.Infrastructure.Persistence;

internal sealed class DatabaseInitializationService(
    IServiceProvider serviceProvider,
    ILogger<DatabaseInitializationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();

        logger.Log(
            LogLevel.Information,
            new EventId(2000, "DatabaseMigrationStarted"),
            "Veritabanı migration kontrolü başlatıldı.",
            null,
            static (state, _) => state);

        await context.Database.MigrateAsync(cancellationToken);

        logger.Log(
            LogLevel.Information,
            new EventId(2001, "DatabaseMigrationCompleted"),
            "Veritabanı migration kontrolü tamamlandı.",
            null,
            static (state, _) => state);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
