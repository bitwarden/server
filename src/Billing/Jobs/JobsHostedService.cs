using System.Runtime.InteropServices;
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
        var timeZone = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
               TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time") :
               TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        if (_globalSettings.SelfHosted)
        {
            timeZone = TimeZoneInfo.Local;
        }

        var everyDayAtNinePmTrigger = TriggerBuilder.Create()
            .WithIdentity("EveryDayAtNinePmTrigger")
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
