using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Utilities;

public class XunitLoggerProvider : ILoggerProvider
{
    private readonly LogLevel _minLevel;
    private readonly DateTimeOffset? _logStart;

    public XunitLoggerProvider(LogLevel minLevel)
        : this(minLevel, null)
    {
    }

    public XunitLoggerProvider(LogLevel minLevel, DateTimeOffset? logStart)
    {
        _minLevel = minLevel;
        _logStart = logStart;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XunitLogger(categoryName, _minLevel, _logStart);
    }

    public void Dispose()
    {
    }
}

public class XunitLogger : ILogger
{
    private static readonly string[] NewLineChars = new[] { Environment.NewLine };
    private readonly string _category;
    private readonly LogLevel _minLogLevel;
    private readonly DateTimeOffset? _logStart;

    public XunitLogger(string category, LogLevel minLogLevel, DateTimeOffset? logStart)
    {
        _minLogLevel = minLogLevel;
        _category = category;
        _logStart = logStart;
    }

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        if (TestContext.Current is not { TestOutputHelper: var testOutputHelper } || testOutputHelper is not { })
        {
            return;
        }

        // Buffer the message into a single string in order to avoid shearing the message when running across multiple threads.
        var messageBuilder = new StringBuilder();

        var timestamp = _logStart.HasValue ?
            $"{(DateTimeOffset.UtcNow - _logStart.Value).TotalSeconds.ToString("N3", CultureInfo.InvariantCulture)}s" :
            DateTimeOffset.UtcNow.ToString("s", CultureInfo.InvariantCulture);

        var firstLinePrefix = $"| [{timestamp}] {_category} {logLevel}: ";
        var lines = formatter(state, exception).Split(NewLineChars, StringSplitOptions.RemoveEmptyEntries);
        messageBuilder.AppendLine(firstLinePrefix + lines.FirstOrDefault() ?? string.Empty);

        var additionalLinePrefix = "|" + new string(' ', firstLinePrefix.Length - 1);
        foreach (var line in lines.Skip(1))
        {
            messageBuilder.AppendLine(additionalLinePrefix + line);
        }

        if (exception != null)
        {
            lines = exception.ToString().Split(NewLineChars, StringSplitOptions.RemoveEmptyEntries);
            additionalLinePrefix = "| ";
            foreach (var line in lines)
            {
                messageBuilder.AppendLine(additionalLinePrefix + line);
            }
        }

        // Remove the last line-break, because ITestOutputHelper only has WriteLine.
        var message = messageBuilder.ToString();
        if (message.EndsWith(Environment.NewLine, StringComparison.Ordinal))
        {
            message = message.Substring(0, message.Length - Environment.NewLine.Length);
        }

        try
        {
            testOutputHelper.WriteLine(message);
        }
        catch (Exception)
        {
            // We could fail because we're on a background thread and our captured ITestOutputHelper is
            // busted (if the test "completed" before the background thread fired).
            // So, ignore this. There isn't really anything we can do but hope the
            // caller has additional loggers registered
        }
    }

    public bool IsEnabled(LogLevel logLevel)
        => logLevel >= _minLogLevel;

    public IDisposable? BeginScope<TState>(TState state)
      where TState : notnull
        => new NullScope();

    private sealed class NullScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
