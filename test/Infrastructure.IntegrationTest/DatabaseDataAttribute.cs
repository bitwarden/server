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
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Bit.Infrastructure.IntegrationTest;

public class DatabaseDataAttribute : DataAttribute
{
    private static readonly Dictionary<string, Type> _seederMap;

    static DatabaseDataAttribute()
    {
        _seederMap = typeof(DatabaseDataAttribute).Assembly.GetCustomAttributesData()
            .Where(ad => ad.AttributeType.IsGenericType
                && ad.AttributeType.GetGenericTypeDefinition() == typeof(SeedConfigurationAttribute<>))
            .ToDictionary(ad => (string)ad.ConstructorArguments[0].Value, ad => ad.AttributeType.GetGenericArguments()[0]);
    }

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
    public string? Seed { get; set; }

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

    public override async ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
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
                // theories.Add(new TheoryDataRow()
                //     .WithTestDisplayName($"{testMethod.DeclaringType.FullName}.{database.Type}.{testMethod.Name}")
                //     .WithSkip("Not-Enabled")
                //     .WithTrait("Database", database.Type.ToString())
                // );
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

            SeedContext? seedContext = null;

            // Could do some async work here
            if (!string.IsNullOrEmpty(Seed))
            {
                if (!_seederMap.TryGetValue(Seed, out var seederType))
                {
                    throw new InvalidOperationException($"No SeederConfigurationAttribute found for seed '{Seed}'");
                }

                var seeder = (ISeeder)ActivatorUtilities.CreateInstance(serviceProvider, seederType);
                disposalTracker.Add(seeder);

                seedContext = new SeedContext();
                await seeder.SeedAsync(seedContext);
            }

            var serviceTheory = new ServiceBasedTheoryDataRow(serviceProvider, testMethod, seedContext)
                .WithTestDisplayName($"{testMethod.DeclaringType.FullName}.{database.Type}.{testMethod.Name}")
                .WithTrait("Database", database.Type.ToString())
                .WithTrait("ConnectionString", database.ConnectionString);
            theories.Add(serviceTheory);
        }

        foreach (var unconfiguredDatabase in unconfiguredDatabases)
        {
            // theories.Add(new TheoryDataRow()
            //     .WithTestDisplayName($"{testMethod.DeclaringType.FullName}.{unconfiguredDatabase}.{testMethod.Name}")
            //     .WithSkip("Unconfigured")
            //     .WithTrait("Database", unconfiguredDatabase.ToString())
            // );
        }

        return theories;
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
        return true;
    }

    private class ServiceBasedTheoryDataRow : TheoryDataRowBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly MethodInfo _testMethod;
        private readonly SeedContext? _seedContext;

        public ServiceBasedTheoryDataRow(IServiceProvider serviceProvider, MethodInfo testMethod, SeedContext? seedContext)
        {
            _serviceProvider = serviceProvider;
            _testMethod = testMethod;
            _seedContext = seedContext;
        }

        protected override object?[] GetData()
        {
            var parameters = _testMethod.GetParameters();

            var services = new object?[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];

                // Special case SeedContext so that we don't have to build services twice.
                if (parameter.ParameterType == typeof(SeedContext))
                {
                    if (_seedContext == null)
                    {
                        throw new InvalidOperationException("This test was not marked with a Seed");
                    }
                    services[i] = _seedContext;
                    continue;
                }

                // TODO: Could support keyed services/optional/nullable
                services[i] = _serviceProvider.GetRequiredService(parameter.ParameterType);
            }

            return services;
        }
    }
}
