using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.Storage;

namespace Bit.AppHost;

public static class BuilderExtensions
{
    /// <summary>
    /// Configures the secrets setup executable resource.
    /// </summary>
    /// <param name="builder">The distributed application builder used to configure the secrets setup resource.</param>
    /// <returns>>The configured resource builder for the secrets setup executable.</returns>
    public static IResourceBuilder<ExecutableResource> ConfigureSecrets(this IDistributedApplicationBuilder builder)
    {
        return builder
            .AddExecutable("setup-secrets", "pwsh", "../dev", "-File", builder.Required("Scripts:SecretsSetup"),
                "-clear")
            .ExcludeFromManifest();
    }

    /// <summary>
    /// Configures the migrations executable resource.
    /// </summary>
    /// <param name="builder">The distributed application builder used to configure the migrations resource.</param>
    /// <returns>>The configured resource builder for the migrations executable.</returns>
    public static IResourceBuilder<ExecutableResource> ConfigureMigrations(this IDistributedApplicationBuilder builder)
    {
        var migrationArgs = new List<string> { "-File", builder.Required("Scripts:DbMigration") };
        if (builder.IsSelfHosted())
            migrationArgs.Add("-self-hosted");

        return builder
            .AddExecutable("run-db-migrations", "pwsh", builder.Required("WorkingDirectory"), migrationArgs.ToArray());
    }

    public static IResourceBuilder<SqlServerDatabaseResource> AddSqlServerDatabaseResource(
        this IDistributedApplicationBuilder builder)
    {
        var isSelfHosted = builder.IsSelfHosted();
        var passwordKey = isSelfHosted ? "Database:SelfHostPassword" : "Database:Password";
        if (!int.TryParse(builder.Required("Database:Port"), out var dbPort))
            throw new InvalidOperationException("Invalid value for Database:Port.");
        var dbPassword = builder.AddParameter("dbPassword", builder.Required(passwordKey), secret: true);
        return builder
            .AddSqlServer("mssql", password: dbPassword, dbPort)
            .WithImage(builder.Required("Database:Image"))
            .WithLifetime(ContainerLifetime.Persistent)
            .WithDataVolume()
            .AddDatabase("vault-db", isSelfHosted ? "self_host_dev" : "vault_dev");
    }

    public static IResourceBuilder<AzureStorageResource> ConfigureAzurite(this IDistributedApplicationBuilder builder)
    {
        // For more information about this configuration: https://github.com/dotnet/aspire/discussions/5552
        var azurite = builder
            .AddAzureStorage("azurite").ConfigureInfrastructure(c =>
            {
                var blobStorage = c.GetProvisionableResources().OfType<BlobService>().SingleOrDefault();
                blobStorage?.CorsRules.Add(new BicepValue<StorageCorsRule>(new StorageCorsRule
                {
                    AllowedOrigins = [new BicepValue<string>("*")],
                    AllowedMethods = [CorsRuleAllowedMethod.Get, CorsRuleAllowedMethod.Put],
                    AllowedHeaders = [new BicepValue<string>("*")],
                    ExposedHeaders = [new BicepValue<string>("*")],
                    MaxAgeInSeconds = new BicepValue<int>("30")
                }));
            })
            .RunAsEmulator(c =>
            {
                c.WithBlobPort(10000)
                    .WithQueuePort(10001)
                    .WithTablePort(10002);
            });

        builder
            .AddExecutable("azurite-setup", "pwsh", builder.Required("WorkingDirectory"), "-File",
                builder.Required("Scripts:AzuriteSetup"))
            .WaitFor(azurite)
            .ExcludeFromManifest();
        return azurite;
    }

    public static IResourceBuilder<ContainerResource> ConfigureMailCatcher(this IDistributedApplicationBuilder builder)
    {
        var image = builder.Required("MailCatcher:Image");
        var imageParts = image.Split(':');
        var imageName = imageParts[0];
        var imageTag = imageParts.Length > 1 ? imageParts[1] : "latest";

        if (!int.TryParse(builder.Required("MailCatcher:SmtpPort"), out var smtpPort))
            throw new InvalidOperationException("Invalid value for MailCatcher:SmtpPort.");
        if (!int.TryParse(builder.Required("MailCatcher:WebPort"), out var webPort))
            throw new InvalidOperationException("Invalid value for MailCatcher:WebPort.");

        return builder
            .AddContainer("mailcatcher", imageName, imageTag)
            .WithLifetime(ContainerLifetime.Persistent)
            .WithEndpoint(port: smtpPort, name: "smtp", targetPort: 1025)
            .WithHttpEndpoint(port: webPort, name: "web", targetPort: webPort);
    }

    /// <summary>
    /// Configures and initializes the essential services required for the distributed application,
    /// including project-specific services such as admin, API, billing, identity, and notifications.
    /// </summary>
    /// <param name="builder">The distributed application builder used to configure resources and services.</param>
    /// <param name="db">The SQL Server database resource builder.</param>
    /// <param name="secretsSetup">The executable resource builder for configuring secrets.</param>
    /// <param name="mail">The container resource builder for setting up the mail service.</param>
    /// <param name="azurite">The Azure Storage resource builder used to configure Azurite storage services.</param>
    /// <returns>A tuple containing the configured resource builders for each project service.</returns>
    public static (
        IResourceBuilder<ProjectResource> Admin,
        IResourceBuilder<ProjectResource> Api,
        IResourceBuilder<ProjectResource> Billing,
        IResourceBuilder<ProjectResource> Identity,
        IResourceBuilder<ProjectResource> Notifications
        ) ConfigureServices(
            this IDistributedApplicationBuilder builder,
            IResourceBuilder<SqlServerDatabaseResource> db,
            IResourceBuilder<ExecutableResource> secretsSetup,
            IResourceBuilder<ContainerResource> mail,
            IResourceBuilder<AzureStorageResource> azurite)
    {
        var admin = builder.AddBitwardenService<Projects.Admin>(db, secretsSetup, mail, "admin");
        var api = builder.AddBitwardenService<Projects.Api>(db, secretsSetup, mail, "api")
            .WaitFor(azurite);
        var billing = builder.AddBitwardenService<Projects.Billing>(db, secretsSetup, mail, "billing");
        var identity = builder.AddBitwardenService<Projects.Identity>(db, secretsSetup, mail, "identity");
        var notifications = builder.AddBitwardenService<Projects.Notifications>(db, secretsSetup, mail, "notifications")
            .WaitFor(azurite);
        builder.ConfigureAdditionalProjects(new Dictionary<string, IResourceBuilder<ProjectResource>>
        {
            ["admin"] = admin,
            ["api"] = api,
            ["billing"] = billing,
            ["identity"] = identity,
            ["notifications"] = notifications
        });
        return (admin, api, billing, identity, notifications);
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

        if (name is "admin" or "identity" or "billing")
            service.WithReference(mail.GetEndpoint("smtp"));

        return service;
    }

    private static int GetBitwardenServicePort(this IDistributedApplicationBuilder builder, string serviceName)
    {
        if (!int.TryParse(builder.Required($"Services:{serviceName}:BasePort"), out var basePort))
            throw new InvalidOperationException($"Invalid port value for Services:{serviceName}:BasePort.");
        return builder.IsSelfHosted() ? basePort + 1 : basePort;
    }

    /// <summary>
    /// Retrieves a required configuration value and throws an exception if it's missing.
    /// </summary>
    /// <param name="builder"> An instance of <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="key"> The configuration key to retrieve.</param>
    /// <returns> The configuration value associated with the specified key.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    private static string Required(this IDistributedApplicationBuilder builder, string key) =>
        builder.Configuration[key] ?? throw new InvalidOperationException($"Missing required configuration: {key}");

    /// <summary>
    /// Determines if the application is running in self-hosted mode.
    /// </summary>
    /// <param name="builder"> An instance of <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <returns> True if the application is self-hosted, otherwise false.</returns>
    private static bool IsSelfHosted(this IDistributedApplicationBuilder builder) =>
        builder.Configuration["SelfHost"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

#if ENABLE_NODEJS_COMMUNITY_PLUGIN
    public static void ConfigureWebFrontend(this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> api)
    {
        if (!int.TryParse(builder.Required("WebFrontend:Port"), out var port))
            throw new InvalidOperationException("Invalid value for WebFrontend:Port.");

        builder
            .AddBitwardenNpmApp("web-frontend", "web", api)
            .WithHttpsEndpoint(port, port, "angular-http", isProxied: false)
            .WithUrl(builder.Required("WebFrontend:Url"))
            .WithExternalHttpEndpoints();
    }

    private static IResourceBuilder<NodeAppResource> AddBitwardenNpmApp(this IDistributedApplicationBuilder builder,
        string name, string path, IResourceBuilder<ProjectResource> api, string scriptName = "build:bit:watch")
    {
        return builder
            .AddNpmApp(name, $"{builder.Required("ClientsPath")}/{path}", scriptName)
            .WithReference(api)
            .WaitFor(api)
            .WithExplicitStart();
    }
#endif

#if ENABLE_NGROK_COMMUNITY_PLUGIN
    public static void ConfigureNgrok(this IDistributedApplicationBuilder builder,
        (IResourceBuilder<ProjectResource>, string) tunnelResource)
    {
        var rawToken = builder.Configuration["NgrokAuthToken"];
        if (string.IsNullOrWhiteSpace(rawToken))
            return;

        var authToken = builder.AddParameter("ngrok-auth-token", rawToken, secret: true);
        builder.AddNgrok("billing-webhook-ngrok-endpoint", endpointPort: 59600)
            .WithAuthToken(authToken)
            .WithTunnelEndpoint(tunnelResource.Item1, tunnelResource.Item2)
            .WithExplicitStart();
    }
#endif
}
