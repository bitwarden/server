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

        var databases = config.GetDatabases();

        foreach (var database in databases)
        {
            if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
            {
                var dapperSqlServerCollection = new ServiceCollection();
                dapperSqlServerCollection.AddLogging(configureLogging);
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
                dapperSqlServerCollection.AddDataProtection();
                yield return dapperSqlServerCollection.BuildServiceProvider();
            }
            else
            {
                var efCollection = new ServiceCollection();
                efCollection.AddLogging(configureLogging);
                efCollection.SetupEntityFramework(database.ConnectionString, database.Type);
                efCollection.AddPasswordManagerEFRepositories(SelfHosted);
                efCollection.AddSingleton(database);
                efCollection.AddDataProtection();
                yield return efCollection.BuildServiceProvider();
            }
        }
    }
}
