using Bit.Core.Settings;
using Bit.Core.Utilities;
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

        builder.AddUrlGroup(identityUri, "identity_server")
            .AddRedis(globalSettings.Redis.ConnectionString)
            .AddAzureQueueStorage(globalSettings.Storage.ConnectionString, name: "storage_queue")
            .AddAzureQueueStorage(globalSettings.Events.ConnectionString, name: "events_queue")
            .AddAzureQueueStorage(globalSettings.Notifications.ConnectionString, name: "notifications_queue")
            .AddAzureServiceBusTopic(s => globalSettings.ServiceBus.ConnectionString,
                s => globalSettings.ServiceBus.ApplicationCacheTopicName, name: "serviceBus");
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

    private static IHealthChecksBuilder AddMailCheck(this IHealthChecksBuilder healthChecksBuilder,
        GlobalSettings globalSettings)
    {
        var awsConfigured = CoreHelpers.SettingHasValue(globalSettings.Amazon?.AccessKeySecret);
        if (awsConfigured && CoreHelpers.SettingHasValue(globalSettings.Mail?.SendGridApiKey))
        {
            //TODO: Add send grid check
            return healthChecksBuilder.AddSendGrid(globalSettings.Mail.SendGridApiKey);
        }

        if (awsConfigured)
        {
            //TODO: Add AWS SES check
            return healthChecksBuilder.AddCheck<AmazonSesHealthCheck>(nameof(AmazonSesHealthCheck));
        }

        //TODO: dev check
        return healthChecksBuilder.AddSmtpHealthCheck(setup =>
        {
            setup.Host = globalSettings.Mail.Smtp.Host;
            setup.Port = globalSettings.Mail.Smtp.Port;
            setup.ConnectionType = SmtpConnectionType.PLAIN;
        }, "dev_mail_server");
    }
}
