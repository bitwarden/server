using Bit.Migrator;

namespace Bit.Infrastructure.IntegrationTest.Services;

/// <summary>
/// An implementation of <see cref="IMigrationTesterService"/> for testing SQL Server migrations.
/// This service applies a specified SQL migration script to a SQL Server database.
/// </summary>
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
