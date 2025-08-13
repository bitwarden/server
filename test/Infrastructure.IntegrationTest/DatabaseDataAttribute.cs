using System.Reflection;
using Bit.Core.Enums;
using Bit.Core.Services;
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
    public string[] EnabledFeatureFlags { get; set; } = [];

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
        // This is for the device repository integration testing.
        var userRequestExpiration = 15;

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
                    PasswordlessAuth = new GlobalSettings.PasswordlessAuthSettings
                    {
                        UserRequestExpiration = TimeSpan.FromMinutes(userRequestExpiration),
                    }
                };
                dapperSqlServerCollection.AddSingleton(globalSettings);
                dapperSqlServerCollection.AddSingleton<IGlobalSettings>(globalSettings);
                dapperSqlServerCollection.AddSingleton(database);
                dapperSqlServerCollection.AddDistributedSqlServerCache(o =>
                {
                    o.ConnectionString = database.ConnectionString;
                    o.SchemaName = "dbo";
                    o.TableName = "Cache";
                });

                dapperSqlServerCollection.AddSingleton<IFeatureService>(new InlineFeatureService(EnabledFeatureFlags));

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

                var globalSettings = new GlobalSettings
                {
                    PasswordlessAuth = new GlobalSettings.PasswordlessAuthSettings
                    {
                        UserRequestExpiration = TimeSpan.FromMinutes(userRequestExpiration),
                    }
                };
                efCollection.AddSingleton(globalSettings);
                efCollection.AddSingleton<IGlobalSettings>(globalSettings);

                efCollection.AddSingleton(database);
                efCollection.AddSingleton<IDistributedCache, EntityFrameworkCache>();

                efCollection.AddSingleton<IFeatureService>(new InlineFeatureService(EnabledFeatureFlags));

                if (!string.IsNullOrEmpty(MigrationName))
                {
                    AddEfMigrationTester(efCollection, database.Type, MigrationName);
                }

                yield return efCollection.BuildServiceProvider();
            }
        }
    }

    // This is a simple offline feature service that honors the EnabledFeatureFlags.
    internal sealed class InlineFeatureService : IFeatureService
    {
        private readonly HashSet<string> _enabled;
        public InlineFeatureService(IEnumerable<string> enabled)
        {
            _enabled = new HashSet<string>(enabled ?? Array.Empty<string>());
        }

        public bool IsOnline() => true;
        public bool IsEnabled(string key, bool defaultValue = false)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }
            return _enabled.Contains(key);
        }
        public int GetIntVariation(string key, int defaultValue = 0) => defaultValue;
        public string GetStringVariation(string key, string defaultValue = null) => defaultValue;
        public Dictionary<string, object> GetAll() => new();
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
}
