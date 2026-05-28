using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Bit.Infrastructure.IntegrationTest;

/// <summary>
/// Records log entries into an in-memory queue so integration tests can assert on
/// what production code logged. Registered alongside <see cref="XUnitLoggerProvider"/>
/// in <see cref="DatabaseDataAttribute"/> and resolvable via DI as a test-method
/// parameter.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_entries, categoryName);

    public void Dispose()
    {
    }

    public readonly record struct LogEntry(
        string Category,
        LogLevel Level,
        string Message,
        Exception? Exception);

    private sealed class CapturingLogger(ConcurrentQueue<LogEntry> entries, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception));
        }
    }
}
