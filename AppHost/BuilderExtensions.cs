using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.Storage;

namespace Bit.AppHost;

public static class BuilderExtensions
{
    public static IResourceBuilder<ExecutableResource> ConfigureSecrets(this IDistributedApplicationBuilder builder)
    {
        // Setup secrets before starting services
        var secretsScript = builder.Configuration["scripts:secretsSetup"] ?? throw new ArgumentNullException("setupSecretsScriptPath", "Missing setup secrets script path");
        var pricingSecretsPath = builder.Configuration["pricingServiceSecretsPath"] ?? throw new ArgumentNullException("pricingServiceSecretsPath", "Missing secrets path");

        //Pricing Secrets
        builder
            .AddExecutable("pricing-setup-secrets", "pwsh", pricingSecretsPath, "-File", secretsScript, "-clear")
            .ExcludeFromManifest();
        return builder
            .AddExecutable("setup-secrets", "pwsh", "../dev", "-File", secretsScript, "-clear")
            .ExcludeFromManifest();
    }

    public static IResourceBuilder<SqlServerDatabaseResource> AddSqlServerDatabaseResource(this IDistributedApplicationBuilder builder, bool isSelfHosted = false)
    {
        var password = isSelfHosted
            ? builder.Configuration["dev:selfHostOverride:globalSettings:sqlServer:password"]
            : builder.Configuration["globalSettings:sqlServer:password"];

        // Add MSSQL - retrieve password from connection string in secrets
        var dbpassword = builder.AddParameter("dbPassword", password!, secret: true);
        return builder
            .AddSqlServer("mssql", password: dbpassword, 1433)
            .WithImage("mssql/server:2022-latest")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithDataVolume()
            .AddDatabase("vault", isSelfHosted ? "self_host_dev" : "vault_dev");
    }

    public static IResourceBuilder<AzureStorageResource> ConfigureAzurite(this IDistributedApplicationBuilder builder)
    {

        // https://github.com/dotnet/aspire/discussions/5552
        var azurite = builder
            .AddAzureStorage("azurite").ConfigureInfrastructure(c =>
            {
                var blobStorage = c.GetProvisionableResources().OfType<BlobService>().Single();
                blobStorage.CorsRules.Add(new BicepValue<StorageCorsRule>(new StorageCorsRule
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
                c.WithBlobPort(10000).
                    WithQueuePort(10001).
                    WithTablePort(10002);
            });

        var workingDirectory = builder.Configuration["workingDirectory"] ?? throw new ArgumentNullException("workingDirectory", "Missing working directory");

        //Run Azurite setup
        var azuriteSetupScript =
            builder
                .Configuration["scripts:azuriteSetup"]
            ?? throw new ArgumentNullException("azuriteSetupScriptPath", "Missing azurite setup script path");

        builder
            .AddExecutable("azurite-setup", "pwsh", workingDirectory, "-File", azuriteSetupScript)
            .WaitFor(azurite)
            .ExcludeFromManifest();
        return azurite;
    }

    public static IResourceBuilder<NgrokResource> ConfigureNgrok(this IDistributedApplicationBuilder builder, (IResourceBuilder<ProjectResource>, string) tunnelResource)
    {
        var authToken = builder
            .AddParameter("ngrok-auth-token",
                builder.Configuration["ngrokAuthToken"]
                ?? throw new ArgumentNullException("ngrokAuthToken", "Missing ngrok auth token"),
                secret: true);

        return builder.AddNgrok("billing-webhook-ngrok-endpoint", endpointPort: 59600)
            .WithAuthToken(authToken)
            .WithTunnelEndpoint(tunnelResource.Item1, tunnelResource.Item2)
            .WithExplicitStart();
    }

    public static IResourceBuilder<ExecutableResource> ConfigureMigrations(this IDistributedApplicationBuilder builder, bool isSelfHosted)
    {
        var workingDirectory = builder.Configuration["workingDirectory"] ??
                               throw new ArgumentNullException("workingDirectory", "Missing working directory");
        var migrationArgs = new List<string>
        {
            "-File",
            builder.Configuration["scripts:dbMigration"]
            ?? throw new ArgumentNullException("migrationScriptPath", "Missing migration script path")
        };
        if (isSelfHosted)
        {
            migrationArgs.Add("-self-hosted");
        }

        return builder
            .AddExecutable("run-db-migrations", "pwsh", workingDirectory, migrationArgs.ToArray());
    }

    public static IResourceBuilder<ProjectResource> AddBitwardenService<TProject>(
        this IDistributedApplicationBuilder builder, IResourceBuilder<SqlServerDatabaseResource> db,
        IResourceBuilder<ExecutableResource> secretsSetup, IResourceBuilder<ContainerResource> mail, string name)
        where TProject : IProjectMetadata, new()
    {
        var service = builder.AddProject<TProject>(name)
            .WithHttpEndpoint(port: builder.GetBitwardenServicePort(name), name: $"{name}-http")
            .WithReference(db)
            .WaitFor(db)
            .WaitForCompletion(secretsSetup);

        if (name is "admin" or "identity" or "billing")
        {
            service.WithReference(mail.GetEndpoint("smtp"));
        }

        return service;
    }

    public static IResourceBuilder<NodeAppResource> AddBitwardenNpmApp(this IDistributedApplicationBuilder builder,
        string name, string path, IResourceBuilder<ProjectResource> api, string scriptName = "build:bit:watch")
    {
        var clientsRelativePath = builder.Configuration["clientsRelativePath"] ??
                                  throw new ArgumentNullException("clientsRelativePath", "Missing client relative path");

        return builder
            .AddNpmApp(name, $"{clientsRelativePath}/{path}", scriptName)
            .WithReference(api)
            .WaitFor(api)
            .WithExplicitStart();
    }

    public static int GetBitwardenServicePort(this IDistributedApplicationBuilder builder, string serviceName)
    {
        var isSelfHosted = builder.Configuration["isSelfHosted"] == "true";
        var configKey = isSelfHosted
            ? $"dev:selfHostOverride:globalSettings:baseServiceUri:{serviceName}"
            : $"globalSettings:baseServiceUri:{serviceName}";

        var uriString = builder.Configuration[configKey]
                        ?? throw new InvalidOperationException($"Configuration value for '{configKey}' not found.");

        return new Uri(uriString).Port;
    }
}
