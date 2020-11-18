using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Api.Jobs
{
    public class JobsHostedService : BaseJobsHostedService
    {
        public JobsHostedService(
            IServiceProvider serviceProvider,
            ILogger<JobsHostedService> logger,
            ILogger<JobListener> listenerLogger)
            : base(serviceProvider, logger, listenerLogger) { }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            var everyTopOfTheHourTrigger = TriggerBuilder.Create()
                .StartNow()
                .WithCronSchedule("0 0 * * * ?")
                .Build();
            var emergencyAccessNotificationTrigger = TriggerBuilder.Create()
                .StartNow()
                .WithCronSchedule("0 * * * * ?")
                .Build();
            var emergencyAccessTimeoutTrigger  = TriggerBuilder.Create()
                .StartNow()
                .WithCronSchedule("0 * * * * ?")
                .Build();
            var everyTopOfTheSixthHourTrigger = TriggerBuilder.Create()
                .StartNow()
                .WithCronSchedule("0 0 */6 * * ?")
                .Build();
            var everyTwelfthHourAndThirtyMinutesTrigger = TriggerBuilder.Create()
                .StartNow()
                .WithCronSchedule("0 30 */12 * * ?")
                .Build();

            Jobs = new List<Tuple<Type, ITrigger>>
            {
                new Tuple<Type, ITrigger>(typeof(AliveJob), everyTopOfTheHourTrigger),
                new Tuple<Type, ITrigger>(typeof(EmergencyAccessNotificationJob), emergencyAccessNotificationTrigger),
                new Tuple<Type, ITrigger>(typeof(EmergencyAccessTimeoutJob), emergencyAccessTimeoutTrigger),
                new Tuple<Type, ITrigger>(typeof(ValidateUsersJob), everyTopOfTheSixthHourTrigger),
                new Tuple<Type, ITrigger>(typeof(ValidateOrganizationsJob), everyTwelfthHourAndThirtyMinutesTrigger)
            };

            await base.StartAsync(cancellationToken);
        }

        public static void AddJobsServices(IServiceCollection services)
        {
            services.AddTransient<AliveJob>();
            services.AddTransient<EmergencyAccessNotificationJob>();
            services.AddTransient<EmergencyAccessTimeoutJob>();
            services.AddTransient<ValidateUsersJob>();
            services.AddTransient<ValidateOrganizationsJob>();
        }
    }
}
