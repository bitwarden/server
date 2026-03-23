using Bit.Core.Jobs;
using Bit.Core.Settings;
using Quartz;

namespace Bit.SeederApi.Jobs;

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
        var everyDayAtMidnightUtc = TriggerBuilder.Create()
            .WithIdentity("EveryDayAtMidnightUtc")
            .StartNow()
            .WithCronSchedule("0 0 0 * * ?")
            .Build();

        var jobs = new List<Tuple<Type, ITrigger>>
        {
            new Tuple<Type, ITrigger>(typeof(AliveJob), everyTopOfTheHourTrigger),
            new Tuple<Type, ITrigger>(typeof(DeleteOldPlayDataJob), everyDayAtMidnightUtc),
        };

        Jobs = jobs;
        await base.StartAsync(cancellationToken);
    }

    public static void AddJobsServices(IServiceCollection services)
    {
        services.AddTransient<AliveJob>();
        services.AddTransient<DeleteOldPlayDataJob>();
    }
}
