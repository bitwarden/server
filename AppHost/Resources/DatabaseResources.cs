namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the persistent SQL Server container and the vault database, choosing the self-hosted variant when configured.
/// </summary>
public static class DatabaseResources
{
    extension(IDistributedApplicationBuilder builder)
    {
        public IResourceBuilder<SqlServerDatabaseResource> AddSqlServerDatabaseResource()
        {
            var isSelfHosted = builder.IsSelfHosted();
            var passwordKey = isSelfHosted ? "Database:SelfHostPassword" : "Database:Password";
            var dbPort = builder.RequiredInt("Database:Port");
            var dbPassword = builder.AddParameter("dbPassword", builder.Required(passwordKey), secret: true);
            return builder
                .AddSqlServer("mssql", password: dbPassword, dbPort)
                .WithImage(builder.Required("Database:Image"))
                .WithLifetime(ContainerLifetime.Persistent)
                .WithDataVolume()
                .AddDatabase("vault-db", isSelfHosted ? "self_host_dev" : "vault_dev");
        }
    }
}
