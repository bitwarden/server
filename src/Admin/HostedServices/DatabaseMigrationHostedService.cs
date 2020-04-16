using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Bit.Core;
using Bit.Core.Jobs;
using Bit.Migrator;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.Admin.HostedServices
{
    public class DatabaseMigrationHostedService : IHostedService, IDisposable
    {
        private readonly GlobalSettings _globalSettings;
        private readonly ILogger<DatabaseMigrationHostedService> _logger;
        private readonly DbMigrator _dbMigrator;

        public DatabaseMigrationHostedService(
            GlobalSettings globalSettings,
            ILogger<DatabaseMigrationHostedService> logger,
            ILogger<DbMigrator> migratorLogger,
            ILogger<JobListener> listenerLogger)
        {
            _globalSettings = globalSettings;
            _logger = logger;
            _dbMigrator = new DbMigrator(globalSettings.SqlServer.ConnectionString, migratorLogger);
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
                    _dbMigrator.MigrateMsSqlDatabase(true, cancellationToken);
                    // TODO: Maybe flip a flag somewhere to indicate migration is complete??
                    break;
                }
                catch (SqlException e)
                {
                    if (i >= maxMigrationAttempts)
                    {
                        _logger.LogError(e, "Database failed to migrate.");
                        throw e;
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
}
