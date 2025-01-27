using Bit.Core.Jobs;
using Bit.Core.Settings;
using Quartz;

namespace Bit.Billing.Jobs;

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

        Jobs = new List<Tuple<Type, ITrigger>>
        {
            new Tuple<Type, ITrigger>(typeof(AliveJob), everyTopOfTheHourTrigger)
        };

        await base.StartAsync(cancellationToken);
    }

    public static void AddJobsServices(IServiceCollection services)
    {
        services.AddTransient<AliveJob>();
        services.AddTransient<SubscriptionCancellationJob>();
    }
}
