using System.Diagnostics.CodeAnalysis;
using Bit.Core.Enums;
using Bit.Core.Settings;

namespace Bit.Api.Utilities;

[ExcludeFromCodeCoverage]
internal static class HealthCheckServices
{
    public static IServiceCollection ConfigureHealthCheckServices(this IServiceCollection services,
        GlobalSettings globalSettings, IHostEnvironment environment)
    {
        var connectionString = GetConnectionString(globalSettings);

        services.AddHealthChecks();
        
        // if(globalSettings.DatabaseProvider)
                

        return services;
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

    // private static IHealthChecksBuilder AddDatabaseCheck(this IHealthChecksBuilder healthChecksBuilder, )
}
