using System.Data;
using System.Reflection;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper;
using Bit.Infrastructure.EntityFramework;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Migrator;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using MySqlConnector;
using Npgsql;
using Xunit.Sdk;

namespace Bit.Infrastructure.IntegrationTest;

public class DatabaseDataAttribute : DataAttribute
{
    public bool SelfHosted { get; set; }
    public bool UseFakeTimeProvider { get; set; }
    public string MigrationName { get; set; }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var parameters = testMethod.GetParameters();

        var config = DatabaseTheoryAttribute.GetConfiguration();

        var serviceProviders = GetDatabaseProviders(config);

        foreach (var provider in serviceProviders)
        {
            var objects = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                objects[i] = provider.GetRequiredService(parameters[i].ParameterType);
            }
            yield return objects;
        }
    }

    protected virtual IEnumerable<IServiceProvider> GetDatabaseProviders(IConfiguration config)
    {
        var configureLogging = (ILoggingBuilder builder) =>
        {
            if (!config.GetValue<bool>("Quiet"))
            {
                builder.AddConfiguration(config);
                builder.AddConsole();
                builder.AddDebug();
            }
        };

        var databases = config.GetDatabases();

        foreach (var database in databases)
        {
            if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
            {
                var dapperSqlServerCollection = new ServiceCollection();
                AddCommonServices(dapperSqlServerCollection, configureLogging);
                dapperSqlServerCollection.AddDapperRepositories(SelfHosted);
                var globalSettings = new GlobalSettings
                {
                    DatabaseProvider = "sqlServer",
                    SqlServer = new GlobalSettings.SqlSettings
                    {
                        ConnectionString = database.ConnectionString,
                    },
                };
                dapperSqlServerCollection.AddSingleton(globalSettings);
                dapperSqlServerCollection.AddSingleton<IGlobalSettings>(globalSettings);
                dapperSqlServerCollection.AddSingleton(database);
                dapperSqlServerCollection.AddDistributedSqlServerCache((o) =>
                {
                    o.ConnectionString = database.ConnectionString;
                    o.SchemaName = "dbo";
                    o.TableName = "Cache";
                });

                if (!string.IsNullOrEmpty(MigrationName))
                {
                    AddSqlMigrationTester(dapperSqlServerCollection, database.ConnectionString, MigrationName);
                }

                yield return dapperSqlServerCollection.BuildServiceProvider();
            }
            else
            {
                var efCollection = new ServiceCollection();
                AddCommonServices(efCollection, configureLogging);
                efCollection.SetupEntityFramework(database.ConnectionString, database.Type);
                efCollection.AddPasswordManagerEFRepositories(SelfHosted);
                efCollection.AddSingleton(database);
                efCollection.AddSingleton<IDistributedCache, EntityFrameworkCache>();

                if (!string.IsNullOrEmpty(MigrationName))
                {
                    AddEfMigrationTester(efCollection, database.Type, MigrationName);
                }

                yield return efCollection.BuildServiceProvider();
            }
        }
    }

    private void AddCommonServices(IServiceCollection services, Action<ILoggingBuilder> configureLogging)
    {
        services.AddLogging(configureLogging);
        services.AddDataProtection();

        if (UseFakeTimeProvider)
        {
            services.AddSingleton<TimeProvider, FakeTimeProvider>();
        }
    }

    private void AddSqlMigrationTester(IServiceCollection services, string connectionString, string migrationName)
    {
        services.AddSingleton<IMigrationTester, SqlMigrationTester>(sp => new SqlMigrationTester(connectionString, migrationName));
    }

    private void AddEfMigrationTester(IServiceCollection services, SupportedDatabaseProviders databaseType, string migrationName)
    {
        services.AddSingleton<IMigrationTester, EfMigrationTester>(sp =>
        {
            var dbContext = sp.GetRequiredService<DatabaseContext>();
            return new EfMigrationTester(dbContext, databaseType, migrationName);
        });
    }
}

public interface IMigrationTester
{
    void ApplyMigration();
}

public class SqlMigrationTester : IMigrationTester
{
    private readonly string _connectionString;
    private readonly string _migrationName;

    public SqlMigrationTester(string connectionString, string migrationName)
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

public class EfMigrationTester : IMigrationTester
{
    private readonly DatabaseContext _databaseContext;
    private readonly SupportedDatabaseProviders _databaseType;
    private readonly string _migrationName;

    public EfMigrationTester(DatabaseContext databaseContext, SupportedDatabaseProviders databaseType, string migrationName)
    {
        _databaseContext = databaseContext;
        _databaseType = databaseType;
        _migrationName = migrationName;
    }

    public void ApplyMigration()
    {
        // Delete the migration history to ensure the migration is applied
        DeleteMigrationHistory();

        var migrator = _databaseContext.GetService<IMigrator>();
        migrator.Migrate(_migrationName);
    }

    private void DeleteMigrationHistory()
    {
        var deleteCommand = "DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE @migrationName";
        IDbDataParameter? parameter = null;

        switch (_databaseType)
        {
            case SupportedDatabaseProviders.MySql:
                parameter = new MySqlParameter("@migrationName", "%" + _migrationName);
                break;
            case SupportedDatabaseProviders.Postgres:
                deleteCommand = "DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE @migrationName";
                parameter = new NpgsqlParameter("@migrationName", "%" + _migrationName);
                break;
            case SupportedDatabaseProviders.Sqlite:
                parameter = new SqliteParameter("@migrationName", "%" + _migrationName);
                break;
        }

        _databaseContext.Database.ExecuteSqlRaw(deleteCommand, parameter);
    }
}
