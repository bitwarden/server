#nullable enable

using System.Security.Cryptography.X509Certificates;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Seeder.Enums;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Seeder.Utilities;

/// <summary>
/// Configures ASP.NET Core Data Protection based on the target environment
/// to ensure seeded data is readable by the target Bitwarden deployment
/// </summary>
public static class DataProtectionConfigurator
{
    public static void ConfigureDataProtection(
        IServiceCollection services,
        GlobalSettings globalSettings,
        SeederEnvironment environment = SeederEnvironment.Auto)
    {
        var builder = services.AddDataProtection()
            .SetApplicationName("Bitwarden");

        // Auto-detect environment if not specified
        if (environment == SeederEnvironment.Auto)
        {
            environment = DetectEnvironment(globalSettings);
        }

        Console.WriteLine($"[Seeder] Configuring data protection for environment: {environment}");

        switch (environment)
        {
            case SeederEnvironment.Development:
                // Default ASP.NET Core behavior - keys in user profile
                Console.WriteLine("[Seeder] Using development data protection (default location)");
                break;

            case SeederEnvironment.SelfHosted:
                var directory = globalSettings.DataProtection?.Directory
                    ?? "/etc/bitwarden/core/aspnet-dataprotection";
                Console.WriteLine($"[Seeder] Using self-hosted data protection: {directory}");

                // Create directory if it doesn't exist
                Directory.CreateDirectory(directory);
                builder.PersistKeysToFileSystem(new DirectoryInfo(directory));
                break;

            case SeederEnvironment.Cloud:
                if (!CoreHelpers.SettingHasValue(globalSettings.Storage?.ConnectionString))
                {
                    throw new InvalidOperationException(
                        "Cloud environment requires Storage.ConnectionString in settings");
                }

                Console.WriteLine("[Seeder] Using cloud data protection with Azure blob storage");

                // Get certificate for key protection
                X509Certificate2? cert = null;
                if (CoreHelpers.SettingHasValue(globalSettings.DataProtection?.CertificateThumbprint))
                {
                    cert = CoreHelpers.GetCertificate(
                        globalSettings.DataProtection.CertificateThumbprint);
                    Console.WriteLine($"[Seeder] Using certificate with thumbprint: {globalSettings.DataProtection.CertificateThumbprint}");
                }
                else if (CoreHelpers.SettingHasValue(globalSettings.DataProtection?.CertificatePassword))
                {
                    cert = CoreHelpers.GetBlobCertificateAsync(
                        globalSettings.Storage.ConnectionString,
                        "certificates",
                        "dataprotection.pfx",
                        globalSettings.DataProtection.CertificatePassword)
                        .GetAwaiter().GetResult();
                    Console.WriteLine("[Seeder] Using certificate from blob storage");
                }

                if (cert == null)
                {
                    throw new InvalidOperationException(
                        "Cloud environment requires a certificate for data protection");
                }

                builder
                    .PersistKeysToAzureBlobStorage(
                        globalSettings.Storage.ConnectionString,
                        "aspnet-dataprotection",
                        "keys.xml")
                    .ProtectKeysWithCertificate(cert);
                break;

            case SeederEnvironment.Ephemeral:
                var ephemeralDir = "/etc/bitwarden/core/aspnet-dataprotection";
                Console.WriteLine($"[Seeder] Using ephemeral environment data protection: {ephemeralDir}");

                // Create directory if it doesn't exist (for local testing)
                Directory.CreateDirectory(ephemeralDir);
                builder.PersistKeysToFileSystem(new DirectoryInfo(ephemeralDir));
                break;
        }
    }

    private static SeederEnvironment DetectEnvironment(GlobalSettings globalSettings)
    {
        // Check for explicit environment variable
        var envVar = Environment.GetEnvironmentVariable("SEEDER_ENVIRONMENT");
        if (!string.IsNullOrEmpty(envVar) && Enum.TryParse<SeederEnvironment>(envVar, out var env))
        {
            Console.WriteLine($"[Seeder] Environment set via SEEDER_ENVIRONMENT: {env}");
            return env;
        }

        // Check for development
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            Console.WriteLine("[Seeder] Detected Development environment");
            return SeederEnvironment.Development;
        }

        // Check self-hosted configurations
        if (globalSettings.SelfHosted)
        {
            // Ephemeral environments use this specific path
            if (globalSettings.DataProtection?.Directory == "/etc/bitwarden/core/aspnet-dataprotection")
            {
                Console.WriteLine("[Seeder] Detected Ephemeral environment (self-hosted with standard path)");
                return SeederEnvironment.Ephemeral;
            }
            Console.WriteLine("[Seeder] Detected Self-hosted environment");
            return SeederEnvironment.SelfHosted;
        }

        // Cloud environment
        if (CoreHelpers.SettingHasValue(globalSettings.Storage?.ConnectionString))
        {
            Console.WriteLine("[Seeder] Detected Cloud environment");
            return SeederEnvironment.Cloud;
        }

        // Default to development if unclear
        Console.WriteLine("[Seeder] Could not detect environment, defaulting to Development");
        return SeederEnvironment.Development;
    }
}
