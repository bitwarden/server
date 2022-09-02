using System.Collections.Specialized;
using Bit.Core.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;

namespace Bit.Core.Jobs;

public abstract class BaseJobsHostedService : IHostedService, IDisposable
{
    private const int MaximumJobRetries = 10;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobListener> _listenerLogger;
    protected readonly ILogger _logger;

    private IScheduler _scheduler;
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

    public IEnumerable<Tuple<Type, ITrigger>> Jobs { get; protected set; }

    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        var props = new NameValueCollection
        {
            {"quartz.serializer.type", "binary"},
        };

        if (!string.IsNullOrEmpty(_globalSettings.SqlServer.JobSchedulerConnectionString))
        {
            // Ensure each project has a unique instanceName
            props.Add("quartz.scheduler.instanceName", GetType().FullName);
            props.Add("quartz.scheduler.instanceId", "AUTO");
            props.Add("quartz.jobStore.type", "Quartz.Impl.AdoJobStore.JobStoreTX");
            props.Add("quartz.jobStore.driverDelegateType", "Quartz.Impl.AdoJobStore.SqlServerDelegate");
            props.Add("quartz.jobStore.useProperties", "true");
            props.Add("quartz.jobStore.dataSource", "default");
            props.Add("quartz.jobStore.tablePrefix", "QRTZ_");
            props.Add("quartz.jobStore.clustered", "true");
            props.Add("quartz.dataSource.default.provider", "SqlServer");
            props.Add("quartz.dataSource.default.connectionString", _globalSettings.SqlServer.JobSchedulerConnectionString);
        }

        var factory = new StdSchedulerFactory(props);
        _scheduler = await factory.GetScheduler(cancellationToken);
        _scheduler.JobFactory = new JobFactory(_serviceProvider);
        _scheduler.ListenerManager.AddJobListener(new JobListener(_listenerLogger),
            GroupMatcher<JobKey>.AnyGroup());
        await _scheduler.Start(cancellationToken);
        if (Jobs != null)
        {
            foreach (var (job, trigger) in Jobs)
            {
                for (var retry = 0; retry < MaximumJobRetries; retry++)
                {
                    // There's a race condition when starting multiple containers simultaneously, retry until it succeeds..
                    try
                    {
                        var dupeT = await _scheduler.GetTrigger(trigger.Key);
                        if (dupeT != null)
                        {
                            await _scheduler.RescheduleJob(trigger.Key, trigger);
                        }

                        var jobDetail = JobBuilder.Create(job)
                            .WithIdentity(job.FullName)
                            .Build();

                        var dupeJ = await _scheduler.GetJobDetail(jobDetail.Key);
                        if (dupeJ != null)
                        {
                            await _scheduler.DeleteJob(jobDetail.Key);
                        }

                        await _scheduler.ScheduleJob(jobDetail, trigger);
                        break;
                    }
                    catch (Exception e)
                    {
                        if (retry == MaximumJobRetries - 1)
                        {
                            throw new Exception("Job failed to start after 10 retries.");
                        }

                        _logger.LogWarning($"Exception while trying to schedule job: {job.FullName}, {e}");
                        var random = new Random();
                        Thread.Sleep(random.Next(50, 250));
                    }
                }
            }
        }

        // Delete old Jobs and Triggers
        var existingJobKeys = await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());
        var jobKeys = Jobs.Select(j =>
        {
            var job = j.Item1;
            return JobBuilder.Create(job)
                .WithIdentity(job.FullName)
                .Build().Key;
        });

        foreach (var key in existingJobKeys)
        {
            if (jobKeys.Contains(key))
            {
                continue;
            }

            _logger.LogInformation($"Deleting old job with key {key}");
            await _scheduler.DeleteJob(key);
        }

        var existingTriggerKeys = await _scheduler.GetTriggerKeys(GroupMatcher<TriggerKey>.AnyGroup());
        var triggerKeys = Jobs.Select(j => j.Item2.Key);

        foreach (var key in existingTriggerKeys)
        {
            if (triggerKeys.Contains(key))
            {
                continue;
            }

            _logger.LogInformation($"Unscheduling old trigger with key {key}");
            await _scheduler.UnscheduleJob(key);
        }
    }

    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        await _scheduler?.Shutdown(cancellationToken);
    }

    public virtual void Dispose()
    { }
}
