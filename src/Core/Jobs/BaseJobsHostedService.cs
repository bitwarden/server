using Bit.Core.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace Bit.Core.Jobs;

#nullable enable

public abstract class BaseJobsHostedService : IHostedService, IDisposable
{
    private const int MaximumJobRetries = 10;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobListener> _listenerLogger;
    protected readonly ILogger _logger;

    private IScheduler? _scheduler;
    protected GlobalSettings _globalSettings;

    public BaseJobsHostedService(
        GlobalSettings globalSettings,
        IServiceProvider serviceProvider,
        ILogger logger,
        ILogger<JobListener> listenerLogger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _listenerLogger = listenerLogger;
        _globalSettings = globalSettings;
    }

    public IEnumerable<Tuple<Type, ITrigger>>? Jobs { get; protected set; }

    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        var fullName = GetType().FullName;
        if (fullName == null)
        {
            throw new InvalidOperationException("Hosted service must have a valid type name.");
        }
        var schedulerBuilder = SchedulerBuilder.Create()
            .WithName(fullName) // Ensure each project has a unique instanceName
            .WithId("AUTO")
            .UseJobFactory<JobFactory>();

        if (!string.IsNullOrEmpty(_globalSettings.SqlServer.JobSchedulerConnectionString))
        {
            schedulerBuilder = schedulerBuilder.UsePersistentStore(options =>
            {
                options.UseProperties = true;
                options.UseClustering();
                options.UseBinarySerializer();
                options.UseSqlServer(connectionString: _globalSettings.SqlServer.JobSchedulerConnectionString);
            });
        }

        var factory = schedulerBuilder.Build();
        _scheduler = await factory.GetScheduler(cancellationToken);

        _scheduler.ListenerManager.AddJobListener(new JobListener(_listenerLogger), GroupMatcher<JobKey>.AnyGroup());

        await _scheduler.Start(cancellationToken);

        var jobKeys = new List<JobKey>();
        var triggerKeys = new List<TriggerKey>();

        if (Jobs != null)
        {
            foreach (var (job, trigger) in Jobs)
            {
                jobKeys.Add(JobBuilder.Create(job)
                    .WithIdentity(job.FullName!)
                    .Build().Key);
                triggerKeys.Add(trigger.Key);

                for (var retry = 0; retry < MaximumJobRetries; retry++)
                {
                    // There's a race condition when starting multiple containers simultaneously, retry until it succeeds..
                    try
                    {
                        var dupeT = await _scheduler.GetTrigger(trigger.Key, cancellationToken);
                        if (dupeT != null)
                        {
                            await _scheduler.RescheduleJob(trigger.Key, trigger, cancellationToken);
                        }

                        var jobDetail = JobBuilder.Create(job)
                            .WithIdentity(job.FullName!)
                            .Build();

                        var dupeJ = await _scheduler.GetJobDetail(jobDetail.Key, cancellationToken);
                        if (dupeJ != null)
                        {
                            await _scheduler.DeleteJob(jobDetail.Key, cancellationToken);
                        }

                        await _scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);
                        break;
                    }
                    catch (Exception e)
                    {
                        if (retry == MaximumJobRetries - 1)
                        {
                            throw new Exception("Job failed to start after 10 retries.");
                        }

                        _logger.LogWarning(e, "Exception while trying to schedule job: {JobName}", job.FullName);
                        var random = new Random();
                        await Task.Delay(random.Next(50, 250));
                    }
                }
            }
        }

        // Delete old Jobs and Triggers
        var existingJobKeys = await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken);

        foreach (var key in existingJobKeys)
        {
            if (jobKeys.Contains(key))
            {
                continue;
            }

            _logger.LogInformation("Deleting old job with key {Key}", key);
            await _scheduler.DeleteJob(key, cancellationToken);
        }

        var existingTriggerKeys = await _scheduler.GetTriggerKeys(GroupMatcher<TriggerKey>.AnyGroup(), cancellationToken);

        foreach (var key in existingTriggerKeys)
        {
            if (triggerKeys.Contains(key))
            {
                continue;
            }

            _logger.LogInformation("Unscheduling old trigger with key {Key}", key);
            await _scheduler.UnscheduleJob(key, cancellationToken);
        }
    }

    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_scheduler is not null)
        {
            await _scheduler.Shutdown(cancellationToken);
        }
    }

    public virtual void Dispose()
    { }
}
