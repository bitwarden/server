using Bit.Core;
using Bit.Core.Jobs;
using Quartz;

namespace Bit.Api.Jobs;

public class AliveJob : BaseJob
{
    public AliveJob(ILogger<AliveJob> logger)
        : base(logger) { }

    protected override Task ExecuteJobAsync(IJobExecutionContext context)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, null, "It's alive!");
        return Task.FromResult(0);
    }
}
