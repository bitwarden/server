using Bit.Core;
using DbUp.Engine.Output;
using Microsoft.Extensions.Logging;

namespace Bit.Migrator;

public class DbUpLogger : IUpgradeLog
{
    private readonly ILogger _logger;

    public DbUpLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void LogTrace(string format, params object[] args)
    {
        _logger.LogTrace(Constants.BypassFiltersEventId, "{TraceMessage}", string.Format(format, args));
    }

    public void LogDebug(string format, params object[] args)
    {
        _logger.LogDebug(Constants.BypassFiltersEventId, "{DebugMessage}", string.Format(format, args));
    }

    public void LogInformation(string format, params object[] args)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, "{InfoMessage}", string.Format(format, args));
    }

    public void LogWarning(string format, params object[] args)
    {
        _logger.LogWarning(Constants.BypassFiltersEventId, "{WarningMessage}", string.Format(format, args));
    }

    public void LogError(string format, params object[] args)
    {
        _logger.LogError(Constants.BypassFiltersEventId, "{ErrorMessage}", string.Format(format, args));
    }

    public void LogError(Exception ex, string format, params object[] args)
    {
        _logger.LogError(Constants.BypassFiltersEventId, ex, "{ErrorMessage}", string.Format(format, args));
    }
}
