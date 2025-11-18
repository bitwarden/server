using System.Reflection;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper;
using Bit.Infrastructure.EntityFramework;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.IntegrationTest.Services;
using Bit.Test.Common;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Bit.Infrastructure.IntegrationTest;

public class DatabaseDataAttribute : DataAttribute
{
    private static IConfiguration? _cachedConfiguration;
    private static IConfiguration GetConfiguration()
    {
        return _cachedConfiguration ??= new ConfigurationBuilder()
            .AddUserSecrets<DatabaseDataAttribute>(optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("BW_TEST_")
            .AddCommandLine(Environment.GetCommandLineArgs())
            .Build();
    }


    public bool SelfHosted { get; set; }
    public bool UseFakeTimeProvider { get; set; }
    public string? MigrationName { get; set; }

    private void AddSqlMigrationTester(IServiceCollection services, string connectionString, string migrationName)
    {
        services.AddSingleton<IMigrationTesterService, SqlMigrationTesterService>(_ => new SqlMigrationTesterService(connectionString, migrationName));
    }

    private void AddEfMigrationTester(IServiceCollection services, SupportedDatabaseProviders databaseType, string migrationName)
    {
        services.AddSingleton<IMigrationTesterService, EfMigrationTesterService>(sp =>
        {
            var dbContext = sp.GetRequiredService<DatabaseContext>();
            return new EfMigrationTesterService(dbContext, databaseType, migrationName);
        });
    }

    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        var config = GetConfiguration();

        HashSet<SupportedDatabaseProviders> unconfiguredDatabases =
        [
            SupportedDatabaseProviders.MySql,
            SupportedDatabaseProviders.Postgres,
            SupportedDatabaseProviders.Sqlite,
            SupportedDatabaseProviders.SqlServer
        ];

        var theories = new List<ITheoryDataRow>();

        foreach (var database in config.GetDatabases())
        {
            unconfiguredDatabases.Remove(database.Type);

            if (!database.Enabled)
            {
                var theory = new TheoryDataRow()
                    .WithSkip("Not-Enabled")
                    .WithTrait("Database", database.Type.ToString());
                theory.Label = database.Type.ToString();
                theories.Add(theory);
                continue;
            }

            var services = new ServiceCollection();
            AddCommonServices(services);

            if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
            {
                // Dapper services
                AddDapperServices(services, database);
            }
            else
            {
                // Ef services
                AddEfServices(services, database);
            }

            var serviceProvider = services.BuildServiceProvider();
            disposalTracker.Add(serviceProvider);

            var serviceTheory = new ServiceBasedTheoryDataRow(serviceProvider, testMethod)
                .WithTrait("Database", database.Type.ToString())
                .WithTrait("ConnectionString", database.ConnectionString);

            serviceTheory.Label = database.Type.ToString();
            theories.Add(serviceTheory);
        }

        foreach (var unconfiguredDatabase in unconfiguredDatabases)
        {
            var theory = new TheoryDataRow()
                .WithSkip("Unconfigured")
                .WithTrait("Database", unconfiguredDatabase.ToString());
            theory.Label = unconfiguredDatabase.ToString();
            theories.Add(theory);
        }

        return new(theories);
    }

    private void AddCommonServices(IServiceCollection services)
    {
        // Common services
        services.AddDataProtection();
        services.AddLogging(logging =>
        {
            logging.AddProvider(new XUnitLoggerProvider());
        });
        if (UseFakeTimeProvider)
        {
            services.AddSingleton<TimeProvider, FakeTimeProvider>();
        }
    }

    private void AddDapperServices(IServiceCollection services, Database database)
    {
        services.AddDapperRepositories(SelfHosted);
        var globalSettings = new GlobalSettings
        {
            DatabaseProvider = "sqlServer",
            SqlServer = new GlobalSettings.SqlSettings
            {
                ConnectionString = database.ConnectionString,
            },
            PasswordlessAuth = new GlobalSettings.PasswordlessAuthSettings
            {
                UserRequestExpiration = TimeSpan.FromMinutes(15),
            }
        };
        services.AddSingleton(globalSettings);
        services.AddSingleton<IGlobalSettings>(globalSettings);
        services.AddSingleton(database);
        services.AddDistributedSqlServerCache(o =>
        {
            o.ConnectionString = database.ConnectionString;
            o.SchemaName = "dbo";
            o.TableName = "Cache";
        });

        if (!string.IsNullOrEmpty(MigrationName))
        {
            AddSqlMigrationTester(services, database.ConnectionString, MigrationName);
        }
    }

    private void AddEfServices(IServiceCollection services, Database database)
    {
        services.SetupEntityFramework(database.ConnectionString, database.Type);
        services.AddPasswordManagerEFRepositories(SelfHosted);

        var globalSettings = new GlobalSettings
        {
            PasswordlessAuth = new GlobalSettings.PasswordlessAuthSettings
            {
                UserRequestExpiration = TimeSpan.FromMinutes(15),
            },
        };
        services.AddSingleton(globalSettings);
        services.AddSingleton<IGlobalSettings>(globalSettings);

        services.AddSingleton(database);
        services.AddSingleton<IDistributedCache, EntityFrameworkCache>();

        if (!string.IsNullOrEmpty(MigrationName))
        {
            AddEfMigrationTester(services, database.Type, MigrationName);
        }
    }

    public override bool SupportsDiscoveryEnumeration()
    {
        return false;
    }

    private class ServiceBasedTheoryDataRow : TheoryDataRowBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly MethodInfo _testMethod;

        public ServiceBasedTheoryDataRow(IServiceProvider serviceProvider, MethodInfo testMethod)
        {
            _serviceProvider = serviceProvider;
            _testMethod = testMethod;
        }

        protected override object?[] GetData()
        {
            var parameters = _testMethod.GetParameters();

            var services = new object?[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                // TODO: Could support keyed services/optional/nullable
                services[i] = _serviceProvider.GetRequiredService(parameter.ParameterType);
            }

            return services;
        }
    }
}
