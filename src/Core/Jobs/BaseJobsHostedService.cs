using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;

namespace Bit.Core.Jobs
{
    public abstract class BaseJobsHostedService : IHostedService, IDisposable
    {
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
            var factory = new StdSchedulerFactory(new NameValueCollection
            {
                {"quartz.serializer.type", "binary"},
                // Ensure each project has a unique instanceName
                {"quartz.scheduler.instanceName", GetType().FullName},
                {"quartz.scheduler.instanceId", "AUTO"},
                {"quartz.jobStore.type", "Quartz.Impl.AdoJobStore.JobStoreTX"},
                {"quartz.jobStore.driverDelegateType", "Quartz.Impl.AdoJobStore.SqlServerDelegate"},
                {"quartz.jobStore.useProperties", "true"},
                {"quartz.jobStore.dataSource", "default"},
                {"quartz.jobStore.tablePrefix", "QRTZ_"},
                {"quartz.jobStore.clustered", "true"},
                {"quartz.dataSource.default.provider", "SqlServer"},
                {"quartz.dataSource.default.connectionString", _globalSettings.SqlServer.ConnectionString},
            });
            _scheduler = await factory.GetScheduler(cancellationToken);
            _scheduler.JobFactory = new JobFactory(_serviceProvider);
            _scheduler.ListenerManager.AddJobListener(new JobListener(_listenerLogger),
                GroupMatcher<JobKey>.AnyGroup());
            await _scheduler.Start(cancellationToken);
            if (Jobs != null)
            {
                foreach (var job in Jobs)
                {
                    var builtJob = JobBuilder.Create(job.Item1).Build();
                    await _scheduler.ScheduleJob(builtJob, job.Item2);
                }
            }
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            await _scheduler?.Shutdown(cancellationToken);
        }

        public virtual void Dispose()
        { }
    }
}
