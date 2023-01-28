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

    public void WriteError(string format, params object[] args)
    {
        _logger.LogError(Constants.BypassFiltersEventId, format, args);
    }

    public void WriteInformation(string format, params object[] args)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, format, args);
    }

    public void WriteWarning(string format, params object[] args)
    {
        _logger.LogWarning(Constants.BypassFiltersEventId, format, args);
    }
}
