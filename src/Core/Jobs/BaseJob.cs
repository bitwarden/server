using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Core.Jobs;

#nullable enable

public abstract class BaseJob : IJob
{
    private static readonly ActivitySource _activitySource = new("Bitwarden.Jobs");

    protected readonly ILogger _logger;

    public BaseJob(ILogger logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var activity = _activitySource.StartActivity(GetType().Name);
        try
        {
            await ExecuteJobAsync(context);
        }
        catch (Exception e)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            _logger.LogError(2, e, "Error performing {0}.", GetType().Name);
        }
    }

    protected abstract Task ExecuteJobAsync(IJobExecutionContext context);
}
