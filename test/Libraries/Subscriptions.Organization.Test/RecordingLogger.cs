using Microsoft.Extensions.Logging;

namespace Bit.Subscriptions.Organization.Test;

internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<string> _errors = [];
    public IReadOnlyList<string> Errors => _errors;
    private readonly List<string> _warnings = [];
    public IReadOnlyList<string> Warnings => _warnings;

    IDisposable? ILogger.BeginScope<TState>(TState state) => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Error)
        {
            _errors.Add(formatter(state, exception));
        }
        else if (logLevel == LogLevel.Warning)
        {
            _warnings.Add(formatter(state, exception));
        }
    }
}
