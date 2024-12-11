using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Core.Jobs;

public class JobListener : IJobListener
{
    private readonly ILogger<JobListener> _logger;

    public JobListener(ILogger<JobListener> logger)
    {
        _logger = logger;
    }

    public string Name => "JobListener";

    public Task JobExecutionVetoed(
        IJobExecutionContext context,
        CancellationToken cancellationToken = default(CancellationToken)
    )
    {
        return Task.FromResult(0);
    }

    public Task JobToBeExecuted(
        IJobExecutionContext context,
        CancellationToken cancellationToken = default(CancellationToken)
    )
    {
        _logger.LogInformation(
            Constants.BypassFiltersEventId,
            null,
            "Starting job {0} at {1}.",
            context.JobDetail.JobType.Name,
            DateTime.UtcNow
        );
        return Task.FromResult(0);
    }

    public Task JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException jobException,
        CancellationToken cancellationToken = default(CancellationToken)
    )
    {
        _logger.LogInformation(
            Constants.BypassFiltersEventId,
            null,
            "Finished job {0} at {1}.",
            context.JobDetail.JobType.Name,
            DateTime.UtcNow
        );
        return Task.FromResult(0);
    }
}
