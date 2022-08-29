using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Core.Jobs;

public abstract class BaseJob : IJob
{
    protected readonly ILogger _logger;

    public BaseJob(ILogger logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await ExecuteJobAsync(context);
        }
        catch (Exception e)
        {
            _logger.LogError(2, e, "Error performing {0}.", GetType().Name);
        }
    }

    protected abstract Task ExecuteJobAsync(IJobExecutionContext context);
}
