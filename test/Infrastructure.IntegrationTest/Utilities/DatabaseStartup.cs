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
using Microsoft.Extensions.Time.Testing;
using Xunit;
using Xunit.v3;
using Xunit.Sdk;

namespace Bit.Infrastructure.IntegrationTest.Utilities;

using TheoryDataBuilder = Func<MethodInfo, DisposalTracker, DatabaseDataAttribute, ITheoryDataRow>;

public class Database
{
    public SupportedDatabaseProviders Type { get; set; }
    public string ConnectionString { get; set; } = default!;
    public bool UseEf { get; set; }
    public bool Enabled { get; set; } = true;
}

internal class TypedConfig
{
    public Database[] Databases { get; set; } = default!;
}

public class DatabaseStartup : ITestPipelineStartup
{
    public static IReadOnlyList<TheoryDataBuilder>? Builders { get; private set; }

    public ValueTask StartAsync(IMessageSink diagnosticMessageSink)
    {
        HashSet<SupportedDatabaseProviders> unconfiguredDatabases =
        [
            SupportedDatabaseProviders.SqlServer,
            SupportedDatabaseProviders.MySql,
            SupportedDatabaseProviders.Postgres,
            SupportedDatabaseProviders.Sqlite
        ];

        // Do startup things
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<DatabaseStartup>(optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("BW_TEST_")
            .AddCommandLine(Environment.GetCommandLineArgs())
            .Build();

        var typedConfig = configuration.Get<TypedConfig>();

        var theories = new List<TheoryDataBuilder>();

        if (typedConfig is not { Databases: var databases })
        {
            foreach (var unconfiguredDatabase in unconfiguredDatabases)
            {
                theories.Add((mi, _, _) => new TheoryDataRow()
                    .WithSkip("Unconfigured")
                    .WithTestDisplayName(TestName(mi, unconfiguredDatabase))
                    .WithTrait("Type", unconfiguredDatabase.ToString()));
            }
            return ValueTask.CompletedTask;
        }

        foreach (var database in databases)
        {
            unconfiguredDatabases.Remove(database.Type);
            if (!database.Enabled)
            {
                theories.Add((mi, _, _) => new TheoryDataRow()
                    .WithSkip($"Disabled")
                    .WithTestDisplayName(TestName(mi, database.Type))
                    .WithTrait("Type", database.Type.ToString())
                    .WithTrait("ConnectionString", database.ConnectionString));
                continue;
            }



            // Build service provider for database
            theories.Add((methodInfo, disposalTracker, databaseDataAttribute) =>
            {
                var sp = BuildServiceProvider(databaseDataAttribute, database);

                return new ServiceTheoryDataRow(methodInfo, disposalTracker, sp)
                    .WithTestDisplayName(TestName(methodInfo, database.Type))
                    .WithTrait("Type", database.Type.ToString())
                    .WithTrait("ConnectionString", database.ConnectionString);
            });
        }

        // Add entry for all still unconfigured database types
        foreach (var unconfiguredDatabase in unconfiguredDatabases)
        {
            theories.Add((mi, _, _) => new TheoryDataRow()
                .WithSkip("Not Configured")
                .WithTestDisplayName(TestName(mi, unconfiguredDatabase))
                .WithTrait("Type", unconfiguredDatabase.ToString()));
        }

        Builders = theories;

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync()
    {
        return ValueTask.CompletedTask;
    }

    private static string TestName(MethodInfo methodInfo, SupportedDatabaseProviders database)
    {
        // Add containing type name to the beginning?
        return $"{methodInfo.Name}({database})";
    }

    private IServiceProvider BuildServiceProvider(DatabaseDataAttribute databaseData, Database database)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {

        });

        services.AddDataProtection();

        if (databaseData.UseFakeTimeProvider)
        {
            services.AddSingleton<TimeProvider, FakeTimeProvider>();
        }

        services.AddSingleton(database);

        if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
        {
            services.AddDapperRepositories(databaseData.SelfHosted);
            var globalSettings = new GlobalSettings
            {
                DatabaseProvider = "sqlServer",
                SqlServer = new GlobalSettings.SqlSettings
                {
                    ConnectionString = database.ConnectionString,
                },
            };
            services.AddSingleton(globalSettings);
            services.AddSingleton<IGlobalSettings>(globalSettings);
            services.AddDistributedSqlServerCache((options) =>
            {
                options.ConnectionString = database.ConnectionString;
                options.SchemaName = "dbo";
                options.TableName = "Cache";
            });

            if (!string.IsNullOrEmpty(databaseData.MigrationName))
            {
                services.AddSingleton<IMigrationTesterService, SqlMigrationTesterService>(
                    sp => new SqlMigrationTesterService(database.ConnectionString, databaseData.MigrationName)
                );
            }
        }
        else
        {
            services.SetupEntityFramework(database.ConnectionString, database.Type);
            services.AddPasswordManagerEFRepositories(databaseData.SelfHosted);
            services.AddSingleton<IDistributedCache, EntityFrameworkCache>();

            if (!string.IsNullOrEmpty(databaseData.MigrationName))
            {
                services.AddSingleton<IMigrationTesterService, EfMigrationTesterService>(sp =>
                {
                    var dbContext = sp.GetRequiredService<DatabaseContext>();
                    return new EfMigrationTesterService(dbContext, database.Type, databaseData.MigrationName);
                });
            }
        }

        return services.BuildServiceProvider();
    }
}
