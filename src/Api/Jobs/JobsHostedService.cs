using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core.Jobs;
using Bit.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Api.Jobs
{
    public class JobsHostedService : BaseJobsHostedService
    {
        public JobsHostedService(
            GlobalSettings globalSettings,
            IServiceProvider serviceProvider,
            ILogger<JobsHostedService> logger,
            ILogger<JobListener> listenerLogger)
            : base(globalSettings, serviceProvider, logger, listenerLogger) { }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            var everyTopOfTheHourTrigger = TriggerBuilder.Create()
                .WithIdentity("EveryTopOfTheHourTrigger")
                .StartNow()
                .WithCronSchedule("0 0 * * * ?")
                .Build();
            var emergencyAccessNotificationTrigger = TriggerBuilder.Create()
                .WithIdentity("EmergencyAccessNotificationTrigger")
                .StartNow()
                .WithCronSchedule("0 0 * * * ?")
                .Build();
            var emergencyAccessTimeoutTrigger = TriggerBuilder.Create()
                .WithIdentity("EmergencyAccessTimeoutTrigger")
                .StartNow()
                .WithCronSchedule("0 0 * * * ?")
                .Build();
            var everyTopOfTheSixthHourTrigger = TriggerBuilder.Create()
                .WithIdentity("EveryTopOfTheSixthHourTrigger")
                .StartNow()
                .WithCronSchedule("0 0 */6 * * ?")
                .Build();
            var everyTwelfthHourAndThirtyMinutesTrigger = TriggerBuilder.Create()
                .WithIdentity("EveryTwelfthHourAndThirtyMinutesTrigger")
                .StartNow()
                .WithCronSchedule("0 30 */12 * * ?")
                .Build();

            var jobs = new List<Tuple<IJobDetail, ITrigger>>
            {
                new Tuple<IJobDetail, ITrigger>(CreateDefaultJob(typeof(AliveJob)), everyTopOfTheHourTrigger),
                new Tuple<IJobDetail, ITrigger>(CreateDefaultJob(typeof(EmergencyAccessNotificationJob)), emergencyAccessNotificationTrigger),
                new Tuple<IJobDetail, ITrigger>(CreateDefaultJob(typeof(EmergencyAccessTimeoutJob)), emergencyAccessTimeoutTrigger),
                new Tuple<IJobDetail, ITrigger>(CreateDefaultJob(typeof(ValidateUsersJob)), everyTopOfTheSixthHourTrigger),
                new Tuple<IJobDetail, ITrigger>(CreateDefaultJob(typeof(ValidateOrganizationsJob)), everyTwelfthHourAndThirtyMinutesTrigger)
            };

            // if (_globalSettings.SelfHosted)
            // {
                jobs.Add(new Tuple<IJobDetail, ITrigger>(CreateDurableJob(typeof(SelfHostedSponsorshipSyncJob)), null));
            // }

            Jobs = jobs;
            await base.StartAsync(cancellationToken);
            await AddTriggerToExistingJob(typeof(SelfHostedSponsorshipSyncJob), CreateRandomDailyTrigger());
        }

        public static void AddJobsServices(IServiceCollection services, bool selfHosted)
        {
            if (selfHosted) 
            {
                services.AddTransient<SelfHostedSponsorshipSyncJob>();
            }
            services.AddTransient<AliveJob>();
            services.AddTransient<EmergencyAccessNotificationJob>();
            services.AddTransient<EmergencyAccessTimeoutJob>();
            services.AddTransient<ValidateUsersJob>();
            services.AddTransient<ValidateOrganizationsJob>();
        }
    }
}
