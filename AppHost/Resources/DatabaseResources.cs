using Bit.AppHost.Configuration;

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the persistent SQL Server container and the vault database, choosing the self-hosted variant when configured.
/// </summary>
internal static class DatabaseResources
{
    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Adds the SQL Server container on a persistent data volume and the vault database inside it.
        /// </summary>
        /// <returns>The resource builder for the vault database.</returns>
        internal IResourceBuilder<SqlServerDatabaseResource> AddSqlServerDatabaseResource()
        {
            var settings = builder.Settings;
            var isSelfHosted = settings.SelfHost;
            var password = isSelfHosted ? settings.Database.SelfHostPassword : settings.Database.Password;
            var dbPassword = builder.AddParameter("dbPassword", password, secret: true);
            return builder
                .AddSqlServer("mssql", password: dbPassword, settings.Database.Port)
                .WithImage(settings.Database.Image)
                .WithLifetime(ContainerLifetime.Persistent)
                .WithDataVolume()
                .AddDatabase("vault-db", isSelfHosted ? "self_host_dev" : "vault_dev");
        }
    }
}
