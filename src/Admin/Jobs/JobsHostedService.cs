using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Admin.Jobs
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
            var everySaturdayAtMidnightTrigger = TriggerBuilder.Create()
                .StartNow()
                .WithCronSchedule("0 0 0 ? * SAT")
                .Build();
            var everySundayAtMidnightTrigger = TriggerBuilder.Create()
                .StartNow()
                .WithCronSchedule("0 0 0 ? * SUN")
                .Build();

            Jobs = new List<Tuple<Type, ITrigger>>
            {
                new Tuple<Type, ITrigger>(typeof(DatabaseUpdateStatisticsJob), everySaturdayAtMidnightTrigger),
                new Tuple<Type, ITrigger>(typeof(DatabaseRebuildlIndexesJob), everySundayAtMidnightTrigger)
            };

            await base.StartAsync(cancellationToken);
        }

        public static void AddJobsServices(IServiceCollection services)
        {
            services.AddTransient<DatabaseUpdateStatisticsJob>();
            services.AddTransient<DatabaseRebuildlIndexesJob>();
        }
    }
}
