using Bit.Core.Jobs;
using Quartz;

namespace Bit.Billing.Jobs;

public class AliveJob(ILogger<AliveJob> logger) : BaseJob(logger)
{
    protected override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Core.Constants.BypassFiltersEventId, null, "Billing service is alive!");
        return Task.FromResult(0);
    }
}
