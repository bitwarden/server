using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core;
using Bit.Core.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Admin.Jobs
{
    public class JobsHostedService : BaseJobsHostedService
    {
        private readonly GlobalSettings _globalSettings;

        public JobsHostedService(
            GlobalSettings globalSettings,
            IServiceProvider serviceProvider,
            ILogger<JobsHostedService> logger,
            ILogger<JobListener> listenerLogger)
            : base(serviceProvider, logger, listenerLogger)
        {
            _globalSettings = globalSettings;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            var timeZone = _globalSettings.SelfHosted ? TimeZoneInfo.Utc :
                TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

            var everyFridayAt10pmTrigger = TriggerBuilder.Create()
                .StartNow()
                .WithCronSchedule("0 0 22 ? * FRI", x => x.InTimeZone(timeZone))
                .Build();
            var everySaturdayAtMidnightTrigger = TriggerBuilder.Create()
                .StartNow()
                .WithCronSchedule("0 0 0 ? * SAT", x => x.InTimeZone(timeZone))
                .Build();
            var everySundayAtMidnightTrigger = TriggerBuilder.Create()
                .StartNow()
                .WithCronSchedule("0 0 0 ? * SUN", x => x.InTimeZone(timeZone))
                .Build();

            Jobs = new List<Tuple<Type, ITrigger>>
            {
                new Tuple<Type, ITrigger>(typeof(DatabaseExpiredGrantsJob), everyFridayAt10pmTrigger),
                new Tuple<Type, ITrigger>(typeof(DatabaseUpdateStatisticsJob), everySaturdayAtMidnightTrigger),
                new Tuple<Type, ITrigger>(typeof(DatabaseRebuildlIndexesJob), everySundayAtMidnightTrigger)
            };

            await base.StartAsync(cancellationToken);
        }

        public static void AddJobsServices(IServiceCollection services)
        {
            services.AddTransient<DatabaseUpdateStatisticsJob>();
            services.AddTransient<DatabaseRebuildlIndexesJob>();
            services.AddTransient<DatabaseExpiredGrantsJob>();
        }
    }
}
