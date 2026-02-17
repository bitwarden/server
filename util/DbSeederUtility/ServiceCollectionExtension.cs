using Bit.Core.Entities;
using Bit.Seeder.Services;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Bit.DbSeederUtility;

public static class ServiceCollectionExtension
{
    public static void ConfigureServices(ServiceCollection services, bool enableMangling = false)
    {
        // Load configuration using the GlobalSettingsFactory
        var globalSettings = GlobalSettingsFactory.GlobalSettings;

        // Register services
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddFilter("Microsoft.EntityFrameworkCore.Model.Validation", LogLevel.Error);
        });
        services.AddSingleton(globalSettings);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.TryAddSingleton<ISeedReader, SeedReader>();

        // Add Data Protection services
        services.AddDataProtection()
            .SetApplicationName("Bitwarden");

        services.AddDatabaseRepositories(globalSettings);

        if (enableMangling)
        {
            services.TryAddScoped<IManglerService, ManglerService>();
        }
        else
        {
            services.TryAddSingleton<IManglerService, NoOpManglerService>();
        }
    }
}
