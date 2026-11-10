namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the script that applies database migrations, passing the self-host flag when configured.
/// </summary>
public static class MigrationResources
{
    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Configures the migrations executable resource.
        /// </summary>
        /// <returns>>The configured resource builder for the migrations executable.</returns>
        public IResourceBuilder<ExecutableResource> ConfigureMigrations()
        {
            var migrationArgs = new List<string> { "-File", builder.Required("Scripts:DbMigration") };
            if (builder.IsSelfHosted())
                migrationArgs.Add("-selfhost");

            return builder
                .AddExecutable("run-db-migrations", "pwsh", builder.Required("WorkingDirectory"), migrationArgs.ToArray());
        }
    }
}
