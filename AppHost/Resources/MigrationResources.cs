namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the script that applies database migrations, passing the self-host flag when configured.
/// </summary>
public static class MigrationResources
{
    /// <summary>
    /// Configures the migrations executable resource.
    /// </summary>
    /// <param name="builder">The distributed application builder used to configure the migrations resource.</param>
    /// <returns>>The configured resource builder for the migrations executable.</returns>
    public static IResourceBuilder<ExecutableResource> ConfigureMigrations(this IDistributedApplicationBuilder builder)
    {
        var migrationArgs = new List<string> { "-File", builder.Required("Scripts:DbMigration") };
        if (builder.IsSelfHosted())
            migrationArgs.Add("-selfhost");

        return builder
            .AddExecutable("run-db-migrations", "pwsh", builder.Required("WorkingDirectory"), [.. migrationArgs]);
    }
}
