using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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
        services.AddSingleton<IGlobalSettings, GlobalSettings>(_ => globalSettings);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.TryAddSingleton<ISeedReader, SeedReader>();

        var dpBuilder = services.AddDataProtection().SetApplicationName("Bitwarden");
        // Persist DataProtection keys to a shared directory when configured, so
        // records this tool encrypts are decryptable by the running app. Needed
        // when seeding an instance whose components share a DataProtection key ring
        // (e.g. self-host). No-op when the directory isn't set.
        if (!string.IsNullOrWhiteSpace(globalSettings.DataProtection.Directory))
        {
            dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(globalSettings.DataProtection.Directory));
        }

        services.AddAttachmentStorageService(globalSettings);

        services.AddDatabaseRepositories(globalSettings);

        if (enableMangling)
        {
            services.TryAddScoped<IManglerService, ManglerService>();
        }
        else
        {
            services.TryAddSingleton<IManglerService, NoOpManglerService>();
        }

        services.TryAddSingleton<IWebHostEnvironment>(new DevelopmentWebHostEnvironment());
        services.AddLicenseServices();
        services.TryAddSingleton<IMailService, NoopMailService>();
        services.AddPush(globalSettings);
        services.TryAddSingleton<ILicensingService, LicensingService>();
    }
}

internal sealed class DevelopmentWebHostEnvironment : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = null!;
    public string ApplicationName { get; set; } = "SeederUtility";
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public string EnvironmentName { get; set; } = Environments.Development;
}
