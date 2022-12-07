using System.Data.SqlClient;
using Bit.Core.Utilities;

namespace Bit.Admin.HostedServices;

public class DatabaseMigrationHostedService : IHostedService, IDisposable
{
    private readonly ILogger<DatabaseMigrationHostedService> _logger;
    private readonly IDbMigrator _dbMigrator;

    public DatabaseMigrationHostedService(
        IDbMigrator dbMigrator,
        ILogger<DatabaseMigrationHostedService> logger)
    {
        _logger = logger;
        _dbMigrator = dbMigrator;
    }

    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        // Wait 20 seconds to allow database to come online
        await Task.Delay(20000);

        var maxMigrationAttempts = 10;
        for (var i = 1; i <= maxMigrationAttempts; i++)
        {
            try
            {
                _dbMigrator.MigrateDatabase(true, cancellationToken);
                // TODO: Maybe flip a flag somewhere to indicate migration is complete??
                break;
            }
            catch (SqlException e)
            {
                if (i >= maxMigrationAttempts)
                {
                    _logger.LogError(e, "Database failed to migrate.");
                    throw;
                }
                else
                {
                    _logger.LogError(e,
                        "Database unavailable for migration. Trying again (attempt #{0})...", i + 1);
                    await Task.Delay(20000);
                }
            }
        }
    }

    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(0);
    }

    public virtual void Dispose()
    { }
}
