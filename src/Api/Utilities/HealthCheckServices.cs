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
                

        return services;
    }

    private static string GetConnectionString(GlobalSettings globalSettings)
    {
        var selectedDatabaseProvider = globalSettings.DatabaseProvider.ToLowerInvariant();

        if (string.IsNullOrEmpty(selectedDatabaseProvider))
        {
            throw new ArgumentNullException();
        }

        return selectedDatabaseProvider switch
        {
            "postgres" or "postgresql" => globalSettings.PostgreSql.ConnectionString,
            "mysql" or "mariadb" => globalSettings.MySql.ConnectionString,
            _ => globalSettings.SqlServer.ConnectionString
        };
    }
}
