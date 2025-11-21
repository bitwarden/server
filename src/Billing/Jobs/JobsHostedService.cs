using Bit.Core.Exceptions;
using Bit.Core.Jobs;
using Bit.Core.Settings;
using Quartz;

namespace Bit.Billing.Jobs;

public class JobsHostedService(
    GlobalSettings globalSettings,
    IServiceProvider serviceProvider,
    ILogger<JobsHostedService> logger,
    ILogger<JobListener> listenerLogger,
    ISchedulerFactory schedulerFactory)
    : BaseJobsHostedService(globalSettings, serviceProvider, logger, listenerLogger)
{
    private List<JobKey> AdHocJobKeys { get; } = [];
    private IScheduler? _adHocScheduler;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        Jobs = new List<Tuple<Type, ITrigger>>
        {
            new(typeof(AliveJob), AliveJob.GetTrigger()),
            new(typeof(ReconcileAdditionalStorageJob), ReconcileAdditionalStorageJob.GetTrigger())
        };

        await base.StartAsync(cancellationToken);
    }

    public static void AddJobsServices(IServiceCollection services)
    {
        services.AddTransient<AliveJob>();
        services.AddTransient<SubscriptionCancellationJob>();
        // add this service as a singleton so we can inject it where needed
        services.AddSingleton<JobsHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<JobsHostedService>());
    }

    public async Task InterruptAdHocJobAsync<T>(CancellationToken cancellationToken = default)
    {
        if (_adHocScheduler == null)
        {
            throw new InvalidOperationException("AdHocScheduler is null, cannot interrupt ad-hoc job.");
        }

        var jobKey = AdHocJobKeys.FirstOrDefault(j => j.Name == typeof(T).ToString());
        if (jobKey == null)
        {
            throw new NotFoundException($"Cannot find job key: {typeof(T)}, not running?");
        }
        logger.LogInformation("CANCELLING ad-hoc job with key: {JobKey}", jobKey);
        AdHocJobKeys.Remove(jobKey);
        await _adHocScheduler.Interrupt(jobKey, cancellationToken);
    }

    public async Task RunJobAdHocAsync<T>(CancellationToken cancellationToken = default) where T : class, IJob
    {
        _adHocScheduler ??= await schedulerFactory.GetScheduler(cancellationToken);

        var jobKey = new JobKey(typeof(T).ToString());

        var currentlyExecuting = await _adHocScheduler.GetCurrentlyExecutingJobs(cancellationToken);
        if (currentlyExecuting.Any(j => j.JobDetail.Key.Equals(jobKey)))
        {
            throw new InvalidOperationException($"Job {jobKey} is already running");
        }

        AdHocJobKeys.Add(jobKey);

        var job = JobBuilder.Create<T>()
            .WithIdentity(jobKey)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(typeof(T).ToString())
            .StartNow()
            .Build();

        logger.LogInformation("Scheduling ad-hoc job with key: {JobKey}", jobKey);

        await _adHocScheduler.ScheduleJob(job, trigger, cancellationToken);
    }
}
