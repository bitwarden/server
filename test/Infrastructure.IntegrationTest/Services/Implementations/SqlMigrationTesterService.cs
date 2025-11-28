using Bit.Migrator;
using Microsoft.Data.SqlClient;

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
        var script = GetMigrationScript(_migrationName);

        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            using (var command = new SqlCommand(script, connection, transaction))
            {
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private string GetMigrationScript(string scriptName)
    {
        var assembly = typeof(DbMigrator).Assembly; ;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith($"{scriptName}.sql"));

        if (resourceName == null)
        {
            throw new FileNotFoundException($"SQL migration script file for '{scriptName}' was not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}
