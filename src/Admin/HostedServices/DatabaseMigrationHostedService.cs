using System.Data.Common;
using Bit.Core.Utilities;
using Microsoft.Extensions.Options;

namespace Bit.Admin.HostedServices;

public class DatabaseMigrationHostedService : IHostedService, IDisposable
{
    private static readonly TimeSpan _migrationDelay = TimeSpan.FromSeconds(20);

    private readonly ILogger<DatabaseMigrationHostedService> _logger;
    private readonly IDbMigrator _dbMigrator;
    private readonly AdminSettings _adminSettings;
    private readonly TimeProvider _timeProvider;

    public DatabaseMigrationHostedService(
        IDbMigrator dbMigrator,
        IOptions<AdminSettings> adminSettings,
        TimeProvider timeProvider,
        ILogger<DatabaseMigrationHostedService> logger)
    {
        _logger = logger;
        _dbMigrator = dbMigrator;
        _adminSettings = adminSettings.Value;
        _timeProvider = timeProvider;
    }

    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_adminSettings.RunDatabaseMigrations)
        {
            return;
        }

        // Wait to allow database to come online
        await Task.Delay(_migrationDelay, _timeProvider, cancellationToken);

        var maxMigrationAttempts = 10;
        for (var i = 1; i <= maxMigrationAttempts; i++)
        {
            try
            {
                _dbMigrator.MigrateDatabase(true, cancellationToken);
                // TODO: Maybe flip a flag somewhere to indicate migration is complete??
                break;
            }
            catch (DbException e)
            {
                if (i >= maxMigrationAttempts)
                {
                    _logger.LogError(e, "Database failed to migrate.");
                    throw;
                }
                else
                {
                    _logger.LogError(e,
                        "Database unavailable for migration. Trying again (attempt #{AttemptNumber})...", i + 1);
                    await Task.Delay(_migrationDelay, _timeProvider, cancellationToken);
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
