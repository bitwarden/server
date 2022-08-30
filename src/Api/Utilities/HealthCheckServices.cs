using System.Diagnostics.CodeAnalysis;
using Bit.Core.Enums;
using Bit.Core.Settings;

namespace Bit.Api.Utilities;

[ExcludeFromCodeCoverage]
internal static class HealthCheckServices
{
    public static void ConfigureHealthCheckServices(this IServiceCollection services,
        GlobalSettings globalSettings, IHostEnvironment environment)
    {
        var identityUri = new Uri(globalSettings.BaseServiceUri.Identity + "/.well-known/openid-configuration");
        
        var heathCheckInitializer = services.AddHealthChecks();

        if (!string.IsNullOrEmpty(GetConnectionString(globalSettings)))
        {
            //add custom db health check
            heathCheckInitializer.AddDatabaseCheck(globalSettings);
        }
        //identity server
        heathCheckInitializer.AddUrlGroup(identityUri, "identity_server");
        //billing api
        //notifications api
    }

    private static string GetConnectionString(GlobalSettings globalSettings)
    {
        var selectedDatabaseProvider = globalSettings.DatabaseProvider.ToLowerInvariant();
        
        return selectedDatabaseProvider switch
        {
            "postgres" or "postgresql" => globalSettings.PostgreSql.ConnectionString,
            "mysql" or "mariadb" => globalSettings.MySql.ConnectionString,
            "sqlserver" => globalSettings.SqlServer.ConnectionString,
            _ => ""
        };
    }

    private static IHealthChecksBuilder AddDatabaseCheck(this IHealthChecksBuilder healthChecksBuilder, 
        GlobalSettings globalSettings)
    {
        var connectionString = GetConnectionString(globalSettings);
        var selectedDatabaseProvider = globalSettings.DatabaseProvider.ToLowerInvariant();

        return selectedDatabaseProvider switch
        {
            "postgres" or "postgresql" => healthChecksBuilder.AddNpgSql(connectionString),
            "mysql" or "mariadb" => healthChecksBuilder.AddMySql(connectionString),
            "sqlserver" => healthChecksBuilder.AddSqlServer(connectionString)
        };
    }
}
