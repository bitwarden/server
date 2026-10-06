using System.Data.Common;
using Bit.Core;
using Bit.Core.Utilities;
using Bit.DataMigrations;

namespace Bit.Admin.HostedServices;

public class DatabaseMigrationHostedService(
    IDbMigrator dbMigrator,
    IDataMigrationRunner dataMigrationRunner,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedService, IDisposable
{
    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        var maxMigrationAttempts = 10;
        for (var i = 1; i <= maxMigrationAttempts; i++)
        {
            try
            {
                dbMigrator.MigrateDatabase(true, name => RunDataMigration(name, cancellationToken), cancellationToken);
                // TODO: Maybe flip a flag somewhere to indicate migration is complete??
                break;
            }
            catch (DbException e)
            {
                if (i >= maxMigrationAttempts)
                {
                    logger.LogError(e, "Database failed to migrate.");
                    throw;
                }
                else
                {
                    logger.LogError(e,
                        "Database unavailable for migration. Trying again (attempt #{AttemptNumber})...", i + 1);
                    await Task.Delay(20000, cancellationToken);
                }
            }
        }
    }

    private bool RunDataMigration(string name, CancellationToken cancellationToken)
    {
        if (!dataMigrationRunner.Names.Contains(name))
        {
            logger.LogError(Constants.BypassFiltersEventId, "Data migration {Name} isn't registered.", name);
            return false;
        }

        logger.LogInformation(Constants.BypassFiltersEventId, "Running data migration {Name}.", name);
        // The generic host has no synchronization context, so blocking here can't deadlock.
        if (dataMigrationRunner.RunToCompletionAsync(name, cancellationToken).GetAwaiter().GetResult())
        {
            return true;
        }

        logger.LogError(Constants.BypassFiltersEventId,
            "Data migration {Name} has failed rows, so later migrations wait until it reruns on the next start.", name);
        return false;
    }

    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(0);
    }

    public virtual void Dispose()
    { }
}
