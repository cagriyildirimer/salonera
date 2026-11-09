using Microsoft.Extensions.Logging;
using Salonera.Infrastructure.Logging;

namespace Salonera.IntegrationTests;

public sealed class DailyFileLoggerTests
{
    [Fact]
    public void InformationMessageIsWrittenToDailyLogFile()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "salonera-tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            using var provider = new DailyFileLoggerProvider(
                new DailyFileLoggerOptions { DirectoryPath = testDirectory });
            var logger = provider.CreateLogger("InfrastructureTest");

            logger.Log(
                LogLevel.Information,
                new EventId(1, "InfrastructureTest"),
                "Altyapı testi tamamlandı.",
                null,
                static (state, _) => state);

            var logFile = Assert.Single(Directory.GetFiles(testDirectory, "salonera-*.log"));
            var content = File.ReadAllText(logFile);
            Assert.Contains("Altyapı testi tamamlandı.", content, StringComparison.Ordinal);
            Assert.Contains("InfrastructureTest", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }
}
