using Bit.Core;
using Bit.Core.Jobs;
using Bit.DataMigrations;
using Bitwarden.Server.Sdk.Features;
using Quartz;

namespace Bit.Admin.Jobs;

/// <summary>
/// Runs every data migration's unpaused partitions on cloud. Self-hosted runs them during the upgrade instead.
/// </summary>
[DisallowConcurrentExecution]
public class DataMigrationsJob(IDataMigrationRunner runner, IFeatureService featureService, ILogger<DataMigrationsJob> logger)
    : BaseJob(logger)
{
    // Under the trigger's interval, so each firing releases its leases and picks up newly started migrations.
    private static readonly TimeSpan _runBudget = TimeSpan.FromMinutes(14);

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.DataMigrations))
        {
            return;
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        budget.CancelAfter(_runBudget);
        var options = new DataMigrationRunOptions(Environment.MachineName, Scheduled: true);
        await Task.WhenAll(runner.Names.Select(async name =>
        {
            try
            {
                await runner.EnsurePartitionsAsync(name, budget.Token);
                await runner.RunAsync(name, options, budget.Token);
            }
            catch (OperationCanceledException) when (budget.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data migration {Name} failed", name);
            }
        }));
    }
}
