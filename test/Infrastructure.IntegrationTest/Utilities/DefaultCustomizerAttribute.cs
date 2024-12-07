
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper;
using Bit.Infrastructure.EntityFramework;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Infrastructure.IntegrationTest.Utilities;

/// <summary>
/// The default customization applied to all database tests. If no customizer is added, this is added implicitly.
/// </summary>
public class DefaultCustomizerAttribute : TestCustomizerAttribute
{
    public static readonly DefaultCustomizerAttribute Instance = new();

    public override Task CustomizeAsync(CustomizationContext customizationContext)
    {
        var database = customizationContext.Database;
        var services = customizationContext.Services;
        if (!database.Enabled)
        {
            // Do nothing
            return Task.CompletedTask;
        }

        services.AddLogging(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(LogLevel.Information));
        });

        services.AddDataProtection();

        services.AddSingleton(customizationContext.Database);

        if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
        {
            services.AddDapperRepositories(false);
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
        }
        else
        {
            services.SetupEntityFramework(database.ConnectionString, database.Type);
            services.AddPasswordManagerEFRepositories(false);
            services.AddSingleton<IDistributedCache, EntityFrameworkCache>();
        }

        return Task.CompletedTask;
    }
}
