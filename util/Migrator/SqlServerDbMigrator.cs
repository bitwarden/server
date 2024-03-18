using System.Data;
using System.Reflection;
using Bit.Core;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using DbUp;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Bit.Migrator;

public class SqlServerDbMigrator : IDbMigrator
{
    private readonly DbMigrator _migrator;

    public SqlServerDbMigrator(GlobalSettings globalSettings)
    {
        _migrator = new DbMigrator(globalSettings.SqlServer.ConnectionString);
    }

    public bool MigrateDatabase(bool enableLogging = true,
        CancellationToken cancellationToken = default)
    {
        return _migrator.MigrateMsSqlDatabaseWithRetries(enableLogging,
            cancellationToken: cancellationToken);
    }
}
