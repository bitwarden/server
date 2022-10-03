using System.Runtime.InteropServices;
using Bit.Core.Jobs;
using Bit.Core.Settings;
using Quartz;

namespace Bit.Admin.Jobs;

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
        var timeZone = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
            TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time") :
            TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        if (_globalSettings.SelfHosted)
        {
            timeZone = TimeZoneInfo.Local;
        }

        var everyTopOfTheHourTrigger = TriggerBuilder.Create()
            .WithIdentity("EveryTopOfTheHourTrigger")
            .StartNow()
            .WithCronSchedule("0 0 * * * ?")
            .Build();
        var everyFiveMinutesTrigger = TriggerBuilder.Create()
            .WithIdentity("EveryFiveMinutesTrigger")
            .StartNow()
            .WithCronSchedule("0 */5 * * * ?")
            .Build();
        var everyFridayAt10pmTrigger = TriggerBuilder.Create()
            .WithIdentity("EveryFridayAt10pmTrigger")
            .StartNow()
            .WithCronSchedule("0 0 22 ? * FRI", x => x.InTimeZone(timeZone))
            .Build();
        var everySaturdayAtMidnightTrigger = TriggerBuilder.Create()
            .WithIdentity("EverySaturdayAtMidnightTrigger")
            .StartNow()
            .WithCronSchedule("0 0 0 ? * SAT", x => x.InTimeZone(timeZone))
            .Build();
        var everySundayAtMidnightTrigger = TriggerBuilder.Create()
            .WithIdentity("EverySundayAtMidnightTrigger")
            .StartNow()
            .WithCronSchedule("0 0 0 ? * SUN", x => x.InTimeZone(timeZone))
            .Build();
        var everyMondayAtMidnightTrigger = TriggerBuilder.Create()
            .WithIdentity("EveryMondayAtMidnightTrigger")
            .StartNow()
            .WithCronSchedule("0 0 0 ? * MON", x => x.InTimeZone(timeZone))
            .Build();
        var everyDayAtMidnightUtc = TriggerBuilder.Create()
            .WithIdentity("EveryDayAtMidnightUtc")
            .StartNow()
            .WithCronSchedule("0 0 0 * * ?")
            .Build();
        var everyFifteenMinutesTrigger = TriggerBuilder.Create()
            .WithIdentity("everyFifteenMinutesTrigger")
            .StartNow()
            .WithCronSchedule("0 */15 * ? * *")
            .Build();

        var jobs = new List<Tuple<Type, ITrigger>>
        {
            new Tuple<Type, ITrigger>(typeof(DeleteSendsJob), everyFiveMinutesTrigger),
            new Tuple<Type, ITrigger>(typeof(DatabaseExpiredGrantsJob), everyFridayAt10pmTrigger),
            new Tuple<Type, ITrigger>(typeof(DatabaseUpdateStatisticsJob), everySaturdayAtMidnightTrigger),
            new Tuple<Type, ITrigger>(typeof(DatabaseRebuildlIndexesJob), everySundayAtMidnightTrigger),
            new Tuple<Type, ITrigger>(typeof(DeleteCiphersJob), everyDayAtMidnightUtc),
            new Tuple<Type, ITrigger>(typeof(DatabaseExpiredSponsorshipsJob), everyMondayAtMidnightTrigger),
            new Tuple<Type, ITrigger>(typeof(DeleteAuthRequestsJob), everyFifteenMinutesTrigger),
        };

        if (!_globalSettings.SelfHosted)
        {
            jobs.Add(new Tuple<Type, ITrigger>(typeof(AliveJob), everyTopOfTheHourTrigger));
        }

        Jobs = jobs;
        await base.StartAsync(cancellationToken);
    }

    public static void AddJobsServices(IServiceCollection services, bool selfHosted)
    {
        if (!selfHosted)
        {
            services.AddTransient<AliveJob>();
        }
        services.AddTransient<DatabaseUpdateStatisticsJob>();
        services.AddTransient<DatabaseRebuildlIndexesJob>();
        services.AddTransient<DatabaseExpiredGrantsJob>();
        services.AddTransient<DatabaseExpiredSponsorshipsJob>();
        services.AddTransient<DeleteSendsJob>();
        services.AddTransient<DeleteCiphersJob>();
        services.AddTransient<DeleteAuthRequestsJob>();
    }
}
