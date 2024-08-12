using Bit.Migrator;

namespace Bit.Infrastructure.IntegrationTest.Services;

public class SqlMigrationTesterService : IMigrationTesterService
{
    private readonly string _connectionString;
    private readonly string _migrationName;

    public SqlMigrationTesterService(string connectionString, string migrationName)
    {
        _connectionString = connectionString;
        _migrationName = migrationName;
    }

    public void ApplyMigration()
    {
        var dbMigrator = new DbMigrator(_connectionString);
        dbMigrator.MigrateMsSqlDatabaseWithRetries(scriptName: _migrationName, repeatable: true);
    }
}
