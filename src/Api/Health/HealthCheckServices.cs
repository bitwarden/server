using System.Diagnostics.CodeAnalysis;
using Bit.Core.Settings;
using HealthChecks.Network.Core;

namespace Bit.Api.Health;

internal static class HealthCheckServices
{
    public static void ConfigureHealthCheckServices(this IServiceCollection services,
        GlobalSettings globalSettings, IHostEnvironment environment)
    {
        var builder = services.AddHealthChecks();
        var identityUri = new Uri(globalSettings.BaseServiceUri.Identity + "/.well-known/openid-configuration");

        if (!string.IsNullOrEmpty(GetConnectionString(globalSettings)))
        {
            //add custom db health check
            builder.AddDatabaseCheck(globalSettings);
        }

        //smtp mail server
        if (environment.IsDevelopment())
        {
            builder.AddSmtpHealthCheck(setup =>
            {
                setup.Host = globalSettings.Mail.Smtp.Host;
                setup.Port = globalSettings.Mail.Smtp.Port;
                setup.ConnectionType = SmtpConnectionType.PLAIN;
            }, "mail_server");
        }

        builder.AddUrlGroup(identityUri, "identity_server")
            .AddRedis(globalSettings.Redis.ConnectionString);
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
            "sqlserver" => healthChecksBuilder.AddSqlServer(connectionString),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
