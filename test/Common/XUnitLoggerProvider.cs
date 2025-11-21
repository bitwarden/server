using Microsoft.Extensions.Logging;
using Xunit;

namespace Bit.Test.Common;

public sealed class XUnitLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new XUnitLogger(categoryName);
    }

    public void Dispose()
    {

    }

    private class XUnitLogger : ILogger
    {
        private readonly string _categoryName;

        public XUnitLogger(string categoryName)
        {
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (TestContext.Current?.TestOutputHelper is not ITestOutputHelper testOutputHelper)
            {
                return;
            }

            testOutputHelper.WriteLine($"[{_categoryName}] {formatter(state, exception)}");
        }
    }
}
