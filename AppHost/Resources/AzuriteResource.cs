using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.Storage;

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the Azurite storage emulator with permissive CORS, plus the script that creates its blob containers,
/// queues and tables.
/// </summary>
public static class AzuriteResource
{
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
            .RunAsEmulator(emulator =>
            {
                emulator
                    .WithBlobPort(10000)
                    .WithQueuePort(10001)
                    .WithTablePort(10002)
                    .WithDataVolume()
                    .WithLifetime(ContainerLifetime.Persistent);
            });

        builder
            .AddExecutable("azurite-setup", "pwsh", builder.Required("WorkingDirectory"), "-File",
                builder.Required("Scripts:AzuriteSetup"))
            .WaitFor(azurite)
            .ExcludeFromManifest();
        return azurite;
    }
}
