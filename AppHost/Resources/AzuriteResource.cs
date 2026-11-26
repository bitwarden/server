using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.Storage;
using Bit.AppHost.Configuration;

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the Azurite storage emulator with permissive CORS, plus the script that creates its blob containers,
/// queues and tables.
/// </summary>
internal static class AzuriteResource
{
    private const int _blobPort = 10000;

    private const int _queuePort = 10001;

    private const int _tablePort = 10002;

    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Adds the persistent Azurite emulator and the <c>azurite-setup</c> script that runs once it is up.
        /// </summary>
        /// <returns>The resource builder for the Azure Storage resource backed by the emulator.</returns>
        internal IResourceBuilder<AzureStorageResource> ConfigureAzurite()
        {
            var settings = builder.Settings;

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
                        .WithBlobPort(_blobPort)
                        .WithQueuePort(_queuePort)
                        .WithTablePort(_tablePort)
                        .WithDataVolume()
                        .WithLifetime(ContainerLifetime.Persistent);
                });

            builder
                .AddExecutable("azurite-setup", "pwsh", settings.WorkingDirectory, "-File",
                    settings.Scripts.AzuriteSetup)
                .WaitFor(azurite)
                .ExcludeFromManifest();
            return azurite;
        }
    }
}
