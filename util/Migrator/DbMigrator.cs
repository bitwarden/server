using System.Data;
using System.Reflection;
using Bit.Core;
using DbUp;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Bit.Migrator;

public class DbMigrator
{
    private readonly string _connectionString;
    private readonly ILogger<DbMigrator> _logger;
    private readonly string _masterConnectionString;

    public DbMigrator(string connectionString, ILogger<DbMigrator> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
        _masterConnectionString = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;
    }

    public bool MigrateMsSqlDatabaseWithRetries(bool enableLogging = true,
        bool rerunable = false,
        string folderName = "DbScripts",
        CancellationToken cancellationToken = default(CancellationToken))
    {
        var attempt = 1;

        while (attempt < 10)
        {
            try
            {
                var success = MigrateDatabase(enableLogging, rerunable, folderName, cancellationToken);
                return success;
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("Server is in script upgrade mode"))
                {
                    attempt++;
                    _logger.LogInformation("Database is in script upgrade mode. " +
                        $"Trying again (attempt #{attempt})...");
                    Thread.Sleep(20000);
                }
                else
                {
                    throw;
                }
            }
        }
        return false;
    }

    public bool MigrateDatabase(bool enableLogging = true,
        bool rerunable = false,
        string folderName = "DbScripts",
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (_logger != null)
        {
            _logger.LogInformation(Constants.BypassFiltersEventId, "Migrating database.");
        }

        using (var connection = new SqlConnection(_masterConnectionString))
        {
            var databaseName = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                databaseName = "vault";
            }

            var databaseNameQuoted = new SqlCommandBuilder().QuoteIdentifier(databaseName);
            var command = new SqlCommand(
                "IF ((SELECT COUNT(1) FROM sys.databases WHERE [name] = @DatabaseName) = 0) " +
                "CREATE DATABASE " + databaseNameQuoted + ";", connection);
            command.Parameters.Add("@DatabaseName", SqlDbType.VarChar).Value = databaseName;
            command.Connection.Open();
            command.ExecuteNonQuery();

            command.CommandText = "IF ((SELECT DATABASEPROPERTYEX([name], 'IsAutoClose') " +
                "FROM sys.databases WHERE [name] = @DatabaseName) = 1) " +
                "ALTER DATABASE " + databaseNameQuoted + " SET AUTO_CLOSE OFF;";
            command.ExecuteNonQuery();
        }

        cancellationToken.ThrowIfCancellationRequested();
        using (var connection = new SqlConnection(_connectionString))
        {
            // Rename old migration scripts to new namespace.
            var command = new SqlCommand(
                "IF OBJECT_ID('Migration','U') IS NOT NULL " +
                "UPDATE [dbo].[Migration] SET " +
                "[ScriptName] = REPLACE([ScriptName], 'Bit.Setup.', 'Bit.Migrator.');", connection);
            command.Connection.Open();
            command.ExecuteNonQuery();
        }

        cancellationToken.ThrowIfCancellationRequested();
        var builder = DeployChanges.To
            .SqlDatabase(_connectionString)
            .JournalRerunableToSqlTable("dbo", "Migration", rerunable)
            .WithScriptsAndCodeEmbeddedInAssembly(Assembly.GetExecutingAssembly(),
                s => s.Contains($".{folderName}.") && !s.Contains(".Archive."))
            .WithTransaction()
            .WithExecutionTimeout(new TimeSpan(0, 5, 0));

        if (enableLogging)
        {
            if (_logger != null)
            {
                builder.LogTo(new DbUpLogger(_logger));
            }
            else
            {
                builder.LogToConsole();
            }
        }

        var upgrader = builder.Build();
        var result = upgrader.PerformUpgrade();

        if (_logger != null)
        {
            if (result.Successful)
            {
                _logger.LogInformation(Constants.BypassFiltersEventId, "Migration successful.");
            }
            else
            {
                _logger.LogError(Constants.BypassFiltersEventId, result.Error, "Migration failed.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return result.Successful;
    }
}
