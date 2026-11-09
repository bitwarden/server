using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Bit.IntegrationTestCommon;

/// <summary>
/// A logger provider that keeps each log entry in memory, so that a test can read the log of a host.
/// </summary>
public sealed class CollectingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CollectedLogEntry> _entries = new();

    /// <summary>
    /// The log entries, in the order that the host wrote them.
    /// </summary>
    public IReadOnlyList<CollectedLogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(_entries);

    public void Dispose()
    {
    }

    private sealed class CollectingLogger(ConcurrentQueue<CollectedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var values = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
            entries.Enqueue(new CollectedLogEntry(logLevel, eventId, formatter(state, exception),
                values.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString())));
        }
    }
}

/// <summary>
/// One log entry that <see cref="CollectingLoggerProvider"/> kept.
/// </summary>
public sealed record CollectedLogEntry(LogLevel Level, EventId EventId, string Message,
    IReadOnlyDictionary<string, string?> Values);
