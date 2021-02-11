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
            var props = new NameValueCollection
            {
                {"quartz.serializer.type", "binary"},
            };

            if (!_globalSettings.SelfHosted && !string.IsNullOrEmpty(_globalSettings.SqlServer.JobSchedulerConnectionString))
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
