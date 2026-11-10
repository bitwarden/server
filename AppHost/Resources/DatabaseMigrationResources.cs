using Bit.AppHost.Configuration;

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the script that applies database migrations, passing the self-host flag when configured.
/// </summary>
internal static class DatabaseMigrationResources
{
    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Configures the migrations executable resource.
        /// </summary>
        /// <returns>>The configured resource builder for the migrations executable.</returns>
        internal IResourceBuilder<ExecutableResource> ConfigureDatabaseMigrations()
        {
            var settings = builder.Settings;
            var migrationArgs = new List<string> { "-File", settings.Scripts.DbMigration };
            if (settings.SelfHost)
                migrationArgs.Add("-selfhost");

            return builder
                .AddExecutable("run-db-migrations", "pwsh", settings.WorkingDirectory, migrationArgs.ToArray());
        }
    }
}
