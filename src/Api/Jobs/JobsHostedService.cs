using System;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;

namespace Bit.Api.Jobs
{
    public class JobsHostedService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly ILogger<JobListener> _listenerLogger;
        private IScheduler _scheduler;

        public JobsHostedService(
            IServiceProvider serviceProvider,
            ILogger<JobsHostedService> logger,
            ILogger<JobListener> listenerLogger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _listenerLogger = listenerLogger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var factory = new StdSchedulerFactory(new NameValueCollection
            {
                { "quartz.serializer.type", "binary" }
            });
            _scheduler = await factory.GetScheduler(cancellationToken);
            _scheduler.JobFactory = new JobFactory(_serviceProvider);
            _scheduler.ListenerManager.AddJobListener(new JobListener(_listenerLogger),
                GroupMatcher<JobKey>.AnyGroup());
            await _scheduler.Start(cancellationToken);

            var aliveJob = JobBuilder.Create<AliveJob>().Build();
            var validateUsersJob = JobBuilder.Create<ValidateUsersJob>().Build();
            var validateOrganizationsJob = JobBuilder.Create<ValidateOrganizationsJob>().Build();

            var everyTopOfTheHourTrigger = TriggerBuilder.Create()
                .StartNow()
                .WithCronSchedule("0 0 * * * ?")
                .Build();
            var everyTopOfTheSixthHourTrigger = TriggerBuilder.Create()
                .StartNow()
                .WithCronSchedule("0 0 */6 * * ?")
                .Build();
            var everyTwelfthHourAndThirtyMinutesTrigger = TriggerBuilder.Create()
                .StartNow()
                .WithCronSchedule("0 30 */12 * * ?")
                .Build();

            await _scheduler.ScheduleJob(aliveJob, everyTopOfTheHourTrigger);
            await _scheduler.ScheduleJob(validateUsersJob, everyTopOfTheSixthHourTrigger);
            await _scheduler.ScheduleJob(validateOrganizationsJob, everyTwelfthHourAndThirtyMinutesTrigger);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _scheduler?.Shutdown(cancellationToken);
        }

        public void Dispose()
        { }

        public static void AddJobsServices(IServiceCollection services)
        {
            services.AddTransient<AliveJob>();
            services.AddTransient<ValidateUsersJob>();
            services.AddTransient<ValidateOrganizationsJob>();
        }
    }
}
