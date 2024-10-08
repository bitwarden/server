using System.Data;
using System.Reflection;
using System.Text;
using Bit.Core;
using DbUp;
using DbUp.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Bit.Migrator;

public class DbMigrator
{
    private readonly string _connectionString;
    private readonly ILogger<DbMigrator> _logger;
    private readonly bool _skipDatabasePreparation;
    private readonly bool _noTransactionMigration;

    public DbMigrator(string connectionString, ILogger<DbMigrator> logger = null,
       bool skipDatabasePreparation = false, bool noTransactionMigration = false)
    {
        _connectionString = connectionString;
        _logger = logger ?? CreateLogger();
        _skipDatabasePreparation = skipDatabasePreparation;
        _noTransactionMigration = noTransactionMigration;
    }

    public bool MigrateMsSqlDatabaseWithRetries(bool enableLogging = true,
        bool repeatable = false,
        string folderName = MigratorConstants.DefaultMigrationsFolderName,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        var attempt = 1;

        while (attempt < 10)
        {
            try
            {
                if (!_skipDatabasePreparation)
                {
                    PrepareDatabase(cancellationToken);
                }

                var success = MigrateDatabase(enableLogging, repeatable, folderName, dryRun, cancellationToken);
                return success;
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("Server is in script upgrade mode."))
                {
                    attempt++;
                    _logger.LogInformation($"Database is in script upgrade mode, trying again (attempt #{attempt}).");
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

    private void PrepareDatabase(CancellationToken cancellationToken = default)
    {
        var masterConnectionString = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        using (var connection = new SqlConnection(masterConnectionString))
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
            // rename old migration scripts to new namespace
            var command = new SqlCommand(
                "IF OBJECT_ID('Migration','U') IS NOT NULL " +
                "UPDATE [dbo].[Migration] SET " +
                "[ScriptName] = REPLACE([ScriptName], 'Bit.Setup.', 'Bit.Migrator.');", connection);
            command.Connection.Open();
            command.ExecuteNonQuery();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private bool MigrateDatabase(bool enableLogging = true,
    bool repeatable = false,
    string folderName = MigratorConstants.DefaultMigrationsFolderName,
    bool dryRun = false,
    CancellationToken cancellationToken = default)
    {
        if (enableLogging)
        {
            _logger.LogInformation(Constants.BypassFiltersEventId, "Migrating database.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var builder = DeployChanges.To
            .SqlDatabase(_connectionString)
            .WithScriptsAndCodeEmbeddedInAssembly(Assembly.GetExecutingAssembly(),
                s => s.Contains($".{folderName}.") && !s.Contains(".Archive."))
            .WithExecutionTimeout(TimeSpan.FromMinutes(5));

        if (_noTransactionMigration)
        {
            builder = builder.WithoutTransaction()
                .WithExecutionTimeout(TimeSpan.FromMinutes(60));
        }
        else
        {
            builder = builder.WithTransaction();
        }

        if (repeatable)
        {
            builder.JournalTo(new NullJournal());
        }
        else
        {
            builder.JournalToSqlTable("dbo", MigratorConstants.SqlTableJournalName);
        }

        if (enableLogging)
        {
            builder.LogTo(new DbUpLogger(_logger));
        }

        var upgrader = builder.Build();

        if (dryRun)
        {
            var scriptsToExec = upgrader.GetScriptsToExecute();
            var stringBuilder = new StringBuilder("Scripts that will be applied:");

            foreach (var script in scriptsToExec)
            {
                stringBuilder.AppendLine(script.Name);
            }
            _logger.LogInformation(Constants.BypassFiltersEventId, stringBuilder.ToString());
            return true;
        }

        var result = upgrader.PerformUpgrade();

        if (enableLogging)
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

    private ILogger<DbMigrator> CreateLogger()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddConsole();

            builder.AddFilter("DbMigrator.DbMigrator", LogLevel.Information);
        });

        return loggerFactory.CreateLogger<DbMigrator>();
    }
}
