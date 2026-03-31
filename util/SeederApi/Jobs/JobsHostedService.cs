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
        var everyFifteenMinutesTrigger = TriggerBuilder.Create()
            .WithIdentity("everyFifteenMinutesTrigger")
            .StartNow()
            .WithCronSchedule("0 */15 * ? * *")
            .Build();


        var jobs = new List<Tuple<Type, ITrigger>>
        {
            new Tuple<Type, ITrigger>(typeof(DeleteOldPlayDataJob), everyFifteenMinutesTrigger),
        };

        Jobs = jobs;
        await base.StartAsync(cancellationToken);
    }

    public static void AddJobsServices(IServiceCollection services)
    {
        services.AddTransient<DeleteOldPlayDataJob>();
    }
}
