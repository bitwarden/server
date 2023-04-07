using System.Reflection;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper;
using Bit.Infrastructure.EntityFramework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Sdk;

namespace Bit.Infrastructure.IntegrationTest;

public class DatabaseDataAttribute : DataAttribute
{
    public bool SelfHosted { get; set; }

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

    private IEnumerable<IServiceProvider> GetDatabaseProviders(IConfiguration config)
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

        if (config.TryGetConnectionString(DatabaseTheoryAttribute.DapperSqlServerKey, out var dapperSqlServerConnectionString))
        {
            var dapperSqlServerCollection = new ServiceCollection();
            dapperSqlServerCollection.AddLogging(configureLogging);
            dapperSqlServerCollection.AddDapperRepositories(SelfHosted);
            var globalSettings = new GlobalSettings
            {
                DatabaseProvider = "sqlServer",
                SqlServer = new GlobalSettings.SqlSettings
                {
                    ConnectionString = dapperSqlServerConnectionString,
                },
            };
            dapperSqlServerCollection.AddSingleton(globalSettings);
            dapperSqlServerCollection.AddSingleton<IGlobalSettings>(globalSettings);
            yield return dapperSqlServerCollection.BuildServiceProvider();
        }

        if (config.TryGetConnectionString(DatabaseTheoryAttribute.EfPostgresKey, out var efPostgresConnectionString))
        {
            var efPostgresCollection = new ServiceCollection();
            efPostgresCollection.AddLogging(configureLogging);
            efPostgresCollection.SetupEntityFramework(efPostgresConnectionString, SupportedDatabaseProviders.Postgres);
            efPostgresCollection.AddPasswordManagerEFRepositories(SelfHosted);
            efPostgresCollection.AddTransient<ITestDatabaseHelper, EfTestDatabaseHelper>();
            yield return efPostgresCollection.BuildServiceProvider();
        }

        if (config.TryGetConnectionString(DatabaseTheoryAttribute.EfMySqlKey, out var efMySqlConnectionString))
        {
            var efMySqlCollection = new ServiceCollection();
            efMySqlCollection.AddLogging(configureLogging);
            efMySqlCollection.SetupEntityFramework(efMySqlConnectionString, SupportedDatabaseProviders.MySql);
            efMySqlCollection.AddPasswordManagerEFRepositories(SelfHosted);
            efMySqlCollection.AddTransient<ITestDatabaseHelper, EfTestDatabaseHelper>();
            yield return efMySqlCollection.BuildServiceProvider();
        }
    }
}
