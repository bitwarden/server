using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole;

/// <summary>
/// Used to test the mssql database only.
/// This is generally NOT what you want and is only used for Flexible Collections which has an opt-in method specific
/// to cloud (and therefore mssql) only. This should be deleted during cleanup so that others don't use it.
/// </summary>
internal class MssqlDatabaseDataAttribute : DatabaseDataAttribute
{
    protected override IEnumerable<IServiceProvider> GetDatabaseProviders(IConfiguration config)
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
        }
    }
}
