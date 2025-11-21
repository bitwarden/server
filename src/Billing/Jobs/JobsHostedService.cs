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
            new(typeof(AliveJob), AliveJob.GetTrigger())
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

        var jobKey = AdHocJobKeys.FirstOrDefault(j => j.Name == nameof(T));
        if (jobKey == null)
        {
            throw new InvalidOperationException($"Cannot find job key: {nameof(T)}");
        }
        await _adHocScheduler.Interrupt(jobKey, cancellationToken);
    }

    public async Task RunJobAdHocAsync<T>(CancellationToken cancellationToken = default) where T : class, IJob
    {
        _adHocScheduler ??= await schedulerFactory.GetScheduler(cancellationToken);

        var jobKey = new JobKey(nameof(T));

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
            .WithIdentity(nameof(T))
            .StartNow()
            .Build();

        logger.LogInformation("Scheduling ad-hoc job with key: {JobKey}", jobKey);

        await _adHocScheduler.ScheduleJob(job, trigger, cancellationToken);
    }
}
