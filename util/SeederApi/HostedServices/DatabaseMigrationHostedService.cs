using System.Data.Common;
using Bit.Core.Utilities;

namespace Bit.SeederApi.HostedServices;

public sealed class DatabaseMigrationHostedService(
    IDbMigrator dbMigrator,
    ILogger<DatabaseMigrationHostedService> logger)
    : IHostedService, IDisposable
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting database migration...");

        // Wait 5 seconds to allow database file to be ready
        await Task.Delay(5000, cancellationToken);

        const int maxMigrationAttempts = 10;
        for (var i = 1; i <= maxMigrationAttempts; i++)
        {
            try
            {
                dbMigrator.MigrateDatabase(true, cancellationToken);
                logger.LogInformation("Database migration completed successfully");
                break;
            }
            catch (DbException e)
            {
                if (i >= maxMigrationAttempts)
                {
                    logger.LogError(e, "Database failed to migrate after {MaxAttempts} attempts", maxMigrationAttempts);
                    throw;
                }

                logger.LogWarning(e,
                    "Database unavailable for migration. Trying again (attempt #{AttemptNumber})...", i + 1);
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(0);
    }

    public void Dispose()
    { }
}
