using Bit.Infrastructure.EntityFramework.Models;
using Bit.Seeder.Enums;
using Bit.Seeder.Services;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.DbSeederUtility;

public static class ServiceCollectionExtension
{
    public static void ConfigureServices(ServiceCollection services)
    {
        ConfigureServices(services, SeederEnvironment.Auto, DatabaseProvider.Auto);
    }

    public static void ConfigureServices(ServiceCollection services,
        SeederEnvironment environment,
        DatabaseProvider database)
    {
        // Load configuration using the GlobalSettingsFactory
        var globalSettings = GlobalSettingsFactory.GlobalSettings;

        // Build configuration for feature flags
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Register services
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton(globalSettings);
        services.AddSingleton<IConfiguration>(configuration);

        // Add Data Protection services
        services.AddDataProtection()
            .SetApplicationName("Bitwarden");

        // Register Seeder services
        services.AddSingleton<IDataProtectionService, DataProtectionService>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        // Register ISeederCryptoService with feature flag support
        // TODO: Implement Rust SDK integration when SDK is ready
        if (configuration.GetValue<bool>("Seeder:UseRustSdk"))
        {
            // Future: Register Rust implementation
            // services.AddSingleton<ISeederCryptoService, RustSeederCryptoService>();

            // For now, throw an error since Rust SDK isn't ready
            throw new NotImplementedException(
                "Rust SDK integration is not yet available. Set 'Seeder:UseRustSdk' to false.");
        }
        else
        {
            // Register C# implementation
            services.AddSingleton<ISeederCryptoService, SeederCryptoService>();
        }

        services.AddDatabaseRepositories(globalSettings);
    }
}
