using Aspire.Hosting.Azure;

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the Bitwarden server projects, wired to the database, secrets setup, mail and storage they depend on,
/// plus any extra projects listed under <c>AdditionalProjects</c> in configuration.
/// </summary>
public static class BitwardenServiceResources
{
    /// <summary>
    /// Configures and initializes the Bitwarden project services required for the distributed
    /// application, keyed by service name.
    /// </summary>
    /// <param name="builder">The distributed application builder used to configure resources and services.</param>
    /// <param name="db">The SQL Server database resource builder.</param>
    /// <param name="secretsSetup">The executable resource builder for configuring secrets.</param>
    /// <param name="mail">The container resource builder for setting up the mail service.</param>
    /// <param name="azurite">The Azure Storage resource builder used to configure Azurite storage services.</param>
    /// <returns>A dictionary of configured project resource builders keyed by service name.</returns>
    public static IReadOnlyDictionary<string, IResourceBuilder<ProjectResource>> ConfigureServices(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<SqlServerDatabaseResource> db,
        IResourceBuilder<ExecutableResource> secretsSetup,
        IResourceBuilder<ContainerResource> mail,
        IResourceBuilder<AzureStorageResource> azurite)
    {
        var services = new Dictionary<string, IResourceBuilder<ProjectResource>>
        {
            ["admin"] = builder.AddBitwardenService<Projects.Admin>(db, secretsSetup, mail, "admin"),
            ["api"] = builder.AddBitwardenService<Projects.Api>(db, secretsSetup, mail, "api")
                .WaitFor(azurite),
            ["billing"] = builder.AddBitwardenService<Projects.Billing>(db, secretsSetup, mail, "billing"),
            ["events"] = builder.AddBitwardenService<Projects.Events>(db, secretsSetup, mail, "events")
                .WaitFor(azurite),
            ["eventsProcessor"] = builder
                .AddBitwardenService<Projects.EventsProcessor>(db, secretsSetup, mail, "eventsProcessor")
                .WaitFor(azurite),
            ["icons"] = builder.AddBitwardenService<Projects.Icons>(db, secretsSetup, mail, "icons"),
            ["identity"] = builder.AddBitwardenService<Projects.Identity>(db, secretsSetup, mail, "identity"),
            ["notifications"] = builder
                .AddBitwardenService<Projects.Notifications>(db, secretsSetup, mail, "notifications")
                .WaitFor(azurite),
            ["scim"] = builder.AddBitwardenService<Projects.Scim>(db, secretsSetup, mail, "scim"),
            ["sso"] = builder.AddBitwardenService<Projects.Sso>(db, secretsSetup, mail, "sso")
        };
        builder.ConfigureAdditionalProjects(services);
        return services;
    }

    /// <summary>
    /// Configures additional projects specified in the configuration under "AdditionalProjects".
    /// This allows for dynamic inclusion of projects without code changes, useful for testing or temporary additions.
    /// </summary>
    /// <param name="builder">The distributed application builder used to access configuration and add project resources.</param>
    /// <param name="services">All registered services keyed by name; each additional project's ReferencedBy list selects which ones receive a reference.</param>
    private static void ConfigureAdditionalProjects(this IDistributedApplicationBuilder builder,
        IReadOnlyDictionary<string, IResourceBuilder<ProjectResource>> services)
    {
        // Add via user-secrets: dotnet user-secrets set "AdditionalProjects:<name>:Path" "<path/to/Project.csproj>"
        foreach (var section in builder.Configuration.GetSection("AdditionalProjects").GetChildren())
        {
            var path = section["Path"];
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var project = builder.AddProject(section.Key, path);
            var referencedBy = section.GetSection("ReferencedBy").GetChildren().Select(c => c.Value).ToHashSet();
            foreach (var (_, service) in services.Where(s => referencedBy.Contains(s.Key)))
                service.WithReference(project);
        }
    }

    /// <summary>
    /// Adds and configures a Bitwarden service of the specified project type. This includes linking the service to
    /// necessary resources such as a database, secrets setup, and optionally a mail service based on the project's name.
    /// </summary>
    /// <param name="builder">The distributed application builder used to configure the service.</param>
    /// <param name="db">The SQL Server database resource to link to the service.</param>
    /// <param name="secretsSetup">The executable resource responsible for secrets setup.</param>
    /// <param name="mail">The container resource representing the mail service, used conditionally for specific projects.</param>
    /// <param name="name">The unique name of the Bitwarden service being added.</param>
    /// <typeparam name="TProject">The type of project implementing the <see cref="IProjectMetadata"/> interface.</typeparam>
    /// <returns>The configured resource builder for the Bitwarden project resource.</returns>
    private static IResourceBuilder<ProjectResource> AddBitwardenService<TProject>(
        this IDistributedApplicationBuilder builder, IResourceBuilder<SqlServerDatabaseResource> db,
        IResourceBuilder<ExecutableResource> secretsSetup, IResourceBuilder<ContainerResource> mail, string name)
        where TProject : IProjectMetadata, new()
    {
        // launchSettings provide the ports for the services
        var service = builder.AddProject<TProject>(name)
            .WithEndpoint("http", e => e.Port = builder.GetBitwardenServicePort(name))
            .WithReference(db)
            .WaitFor(db)
            .WaitForCompletion(secretsSetup);

        if (builder.IsSelfHosted())
        {
            service.WithEnvironment("developSelfHosted", "true");
        }

        if (name is "admin" or "identity" or "billing" or "sso")
            service.WithReference(mail.GetEndpoint("smtp"));

        return service;
    }
}
