using System;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading;
using DbUp;
using Microsoft.Extensions.Logging;

namespace Bit.Migrator
{
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

        public bool MigrateMsSqlDatabase(bool enableLogging = true,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if(enableLogging && _logger != null)
            {
                _logger.LogInformation("Migrating database.");
            }

            using(var connection = new SqlConnection(_masterConnectionString))
            {
                var command = new SqlCommand(
                    "IF ((SELECT COUNT(1) FROM sys.databases WHERE [name] = 'vault') = 0) " +
                    "CREATE DATABASE [vault];", connection);
                command.Connection.Open();
                command.ExecuteNonQuery();

                command.CommandText = "IF ((SELECT DATABASEPROPERTYEX([name], 'IsAutoClose') " +
                    "FROM sys.databases WHERE [name] = 'vault') = 1) " +
                    "ALTER DATABASE [vault] SET AUTO_CLOSE OFF;";
                command.ExecuteNonQuery();
            }

            using(var connection = new SqlConnection(_connectionString))
            {
                // Rename old migration scripts to new namespace.
                var command = new SqlCommand(
                    "IF OBJECT_ID('Migration','U') IS NOT NULL " +
                    "UPDATE [dbo].[Migration] SET " +
                    "[ScriptName] = REPLACE([ScriptName], '.Setup.', '.Migrator.');", connection);
                command.Connection.Open();
                command.ExecuteNonQuery();
            }

            var builder = DeployChanges.To
                .SqlDatabase(_connectionString)
                .JournalToSqlTable("dbo", "Migration")
                .WithScriptsAndCodeEmbeddedInAssembly(Assembly.GetExecutingAssembly(),
                    s => s.Contains($".DbScripts.") && !s.Contains(".Archive."))
                .WithTransaction()
                .WithExecutionTimeout(new TimeSpan(0, 5, 0));

            if(enableLogging)
            {
                if(_logger != null)
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

            if(enableLogging && _logger != null)
            {
                if(result.Successful)
                {
                    _logger.LogInformation("Migration successful.");
                }
                else
                {
                    _logger.LogError(result.Error, "Migration failed.");
                }
            }

            return result.Successful;
        }
    }
}
