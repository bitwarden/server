using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Billing.Jobs
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
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

            var everyDayAtNinePmTrigger = TriggerBuilder.Create()
                .StartNow()
                .WithCronSchedule("0 0 21 * * ?", x => x.InTimeZone(timeZone))
                .Build();

            Jobs = new List<Tuple<Type, ITrigger>>();

            // Add jobs here

            await base.StartAsync(cancellationToken);
        }

        public static void AddJobsServices(IServiceCollection services)
        {
            // Register jobs here
        }
    }
}
