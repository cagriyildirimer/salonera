using Microsoft.Extensions.Logging;

namespace Salonera.Infrastructure.Logging;

public sealed class DailyFileLoggerOptions
{
    public string DirectoryPath { get; init; } = "logs";

    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;
}
