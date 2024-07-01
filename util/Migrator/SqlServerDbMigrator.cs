using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Migrator;

public class SqlServerDbMigrator : IDbMigrator
{
    private readonly DbMigrator _migrator;

    public SqlServerDbMigrator(GlobalSettings globalSettings, ILogger<DbMigrator> logger)
    {
        _migrator = new DbMigrator(globalSettings.SqlServer.ConnectionString, logger,
            globalSettings.SqlServer.SkipDatabasePreparation);
    }

    public bool MigrateDatabase(bool enableLogging = true,
        CancellationToken cancellationToken = default)
    {
        return _migrator.MigrateMsSqlDatabaseWithRetries(enableLogging,
            cancellationToken: cancellationToken);
    }
}
