using Bit.Core;
using Bit.Core.Jobs;
using Quartz;

namespace Bit.Notifications.Jobs;

public class LogConnectionCounterJob : BaseJob
{
    private readonly ConnectionCounter _connectionCounter;

    public LogConnectionCounterJob(
        ILogger<LogConnectionCounterJob> logger,
        ConnectionCounter connectionCounter)
        : base(logger)
    {
        _connectionCounter = connectionCounter;
    }

    protected override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Connection count for server {0}: {1}", Environment.MachineName, _connectionCounter.GetCount());
        return Task.FromResult(0);
    }
}
