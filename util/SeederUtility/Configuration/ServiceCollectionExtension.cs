using System.IO;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Seeder.Services;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Bit.SeederUtility.Configuration;

public static class ServiceCollectionExtension
{
    public static void ConfigureServices(ServiceCollection services, bool enableMangling = false)
    {
        var globalSettings = GlobalSettingsFactory.GlobalSettings;

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddFilter("Microsoft.EntityFrameworkCore.Model.Validation", LogLevel.Error);
        });
        services.AddSingleton(globalSettings);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.TryAddSingleton<ISeedReader, SeedReader>();

        var dataProtectionBuilder = services.AddDataProtection().SetApplicationName("Bitwarden");
        if (globalSettings.SelfHosted && CoreHelpers.SettingHasValue(globalSettings.DataProtection.Directory))
        {
            dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(globalSettings.DataProtection.Directory));
        }

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
