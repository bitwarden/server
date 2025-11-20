using Bit.Core.Jobs;
using Bit.Core.Settings;
using Quartz;

namespace Bit.Billing.Jobs;

public class ReconcileAdditionalStorageJobHostedService(
    GlobalSettings globalSettings,
    IServiceProvider serviceProvider,
    ILogger<ReconcileAdditionalStorageJobHostedService> logger,
    ILogger<JobListener> listenerLogger)
    : BaseJobsHostedService(globalSettings, serviceProvider, logger, listenerLogger)
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        Jobs = new List<Tuple<Type, ITrigger>>
        {
            new(typeof(ReconcileAdditionalStorageJob), ReconcileAdditionalStorageJob.GetTrigger()),
        };

        await base.StartAsync(cancellationToken);
    }

    public static void AddJobsServices(IServiceCollection services)
    {
        services.AddTransient<ReconcileAdditionalStorageJob>();
        services.AddSingleton<ReconcileAdditionalStorageJobHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<ReconcileAdditionalStorageJobHostedService>());
    }

}
