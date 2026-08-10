#nullable enable

using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Core.Jobs.DataMigrations;

/// <summary>
/// The single scheduler entry point for all gradual data migrations: one trigger runs every
/// registered <see cref="IDataMigration"/> per firing. Migrations self-gate (feature flag /
/// completion state / partition claims), so a firing with nothing to do costs one state-table
/// read per migration. Cloud and self-hosted behave identically — the per-migration feature flag
/// is the only switch.
///
/// <para>Firings deliberately may overlap — the same concurrency the state table already
/// tolerates across instances (READPAST claims, owner-fenced checkpoints, CAS data writes).
/// An overlapping firing tops idle drain capacity back up to each migration's cap and refreshes
/// the pending gauge, then exits; the engine bounds in-process parallelism across firings.</para>
/// </summary>
public class DataMigrationsJob : BaseJob
{
    private readonly IEnumerable<IDataMigration> _migrations;

    public DataMigrationsJob(
        IEnumerable<IDataMigration> migrations,
        ILogger<DataMigrationsJob> logger)
        : base(logger)
    {
        _migrations = migrations;
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        // Concurrent, not sequential: each migration drains its claimed partitions for as long as
        // work remains, so running them one after another would let a large table starve every
        // migration behind it for days.
        await Task.WhenAll(_migrations.Select(async migration =>
        {
            try
            {
                await migration.RunAsync(context.CancellationToken);
            }
            catch (Exception e)
            {
                // Isolate: one migration failing must not starve the others.
                _logger.LogError(e, "Data migration {Name} failed this firing.", migration.Name);
            }
        }));
    }
}
