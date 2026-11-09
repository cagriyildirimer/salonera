using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Salonera.Infrastructure.Logging;

/// <summary>
/// Günlük bazında dönen sade dosya log sağlayıcısıdır. Log mesajlarında parola,
/// erişim anahtarı ve kişisel veri kullanılmamalıdır.
/// </summary>
public sealed class DailyFileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, DailyFileLogger> _loggers = new(StringComparer.Ordinal);
    private readonly DailyFileLoggerOptions _options;
    private readonly Lock _writeLock = new();
    private bool _disposed;

    public DailyFileLoggerProvider(DailyFileLoggerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);

        return _loggers.GetOrAdd(categoryName, name => new DailyFileLogger(name, _options, WriteLine));
    }

    public void Dispose()
    {
        _disposed = true;
        _loggers.Clear();
    }

    private void WriteLine(string line)
    {
        var directoryPath = Path.GetFullPath(_options.DirectoryPath);
        Directory.CreateDirectory(directoryPath);

        var fileName = $"salonera-{DateTime.UtcNow:yyyyMMdd}.log";
        var filePath = Path.Combine(directoryPath, fileName);

        lock (_writeLock)
        {
            File.AppendAllText(filePath, line + Environment.NewLine);
        }
    }

    private sealed class DailyFileLogger(
        string categoryName,
        DailyFileLoggerOptions options,
        Action<string> writeLine) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= options.MinimumLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);

            var message = formatter(state, exception);
            var exceptionText = exception is null ? string.Empty : $" | {exception}";
            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTimeOffset.UtcNow:O} [{logLevel}] {categoryName} ({eventId.Id}) {message}{exceptionText}");

            writeLine(line);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
