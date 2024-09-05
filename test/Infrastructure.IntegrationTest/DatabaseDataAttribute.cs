using System.Reflection;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper;
using Bit.Infrastructure.EntityFramework;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.IntegrationTest.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit.Sdk;

namespace Bit.Infrastructure.IntegrationTest;

public class DatabaseDataAttribute : DataAttribute
{
    public bool SelfHosted { get; set; }
    public bool UseFakeTimeProvider { get; set; }
    public string? MigrationName { get; set; }

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
        services.AddSingleton<IMigrationTesterService, SqlMigrationTesterService>(sp => new SqlMigrationTesterService(connectionString, migrationName));
    }

    private void AddEfMigrationTester(IServiceCollection services, SupportedDatabaseProviders databaseType, string migrationName)
    {
        services.AddSingleton<IMigrationTesterService, EfMigrationTesterService>(sp =>
        {
            var dbContext = sp.GetRequiredService<DatabaseContext>();
            return new EfMigrationTesterService(dbContext, databaseType, migrationName);
        });
    }
}
