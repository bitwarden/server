using Bit.Core.Jobs;
using Bit.Core.Settings;
using Quartz;

namespace Bit.Notifications.Jobs;

public class JobsHostedService : BaseJobsHostedService
{
    public JobsHostedService(
        GlobalSettings globalSettings,
        IServiceProvider serviceProvider,
        ILogger<JobsHostedService> logger,
        ILogger<JobListener> listenerLogger
    )
        : base(globalSettings, serviceProvider, logger, listenerLogger) { }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var everyFiveMinutesTrigger = TriggerBuilder
            .Create()
            .WithIdentity("EveryFiveMinutesTrigger")
            .StartNow()
            .WithCronSchedule("0 */30 * * * ?")
            .Build();

        Jobs = new List<Tuple<Type, ITrigger>>
        {
            new Tuple<Type, ITrigger>(typeof(LogConnectionCounterJob), everyFiveMinutesTrigger),
        };

        await base.StartAsync(cancellationToken);
    }

    public static void AddJobsServices(IServiceCollection services)
    {
        services.AddTransient<LogConnectionCounterJob>();
    }
}
