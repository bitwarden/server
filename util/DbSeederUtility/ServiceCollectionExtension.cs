using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.DbSeederUtility;

public static class ServiceCollectionExtension
{
    public static void ConfigureServices(ServiceCollection services)
    {
        // Load configuration using the GlobalSettingsFactory
        var globalSettings = GlobalSettingsFactory.GlobalSettings;

        // Register services
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton(globalSettings);

        // Add Data Protection services
        services.AddDataProtection()
            .SetApplicationName("Bitwarden");

        services.AddDatabaseRepositories(globalSettings);
    }
}
