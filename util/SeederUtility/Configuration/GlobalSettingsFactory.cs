using Bit.Core.Settings;
using Microsoft.Extensions.Configuration;

namespace Bit.SeederUtility.Configuration;

public static class GlobalSettingsFactory
{
    private static GlobalSettings? _globalSettings;

    public static GlobalSettings GlobalSettings
    {
        get { return _globalSettings ??= LoadGlobalSettings(); }
    }

    private static GlobalSettings LoadGlobalSettings()
    {
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            // Default to Development because SeederUtility has no production deployment —
            // it's a local dev tool. This matches the convention used by every other project
            // for storing dev-only config (e.g. pricingUri) under appsettings.Development.json.
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true, reloadOnChange: true)
            .AddUserSecrets("bitwarden-Api")
            .AddEnvironmentVariables();

        var configuration = configBuilder.Build();
        var globalSettingsSection = configuration.GetSection("globalSettings");

        var settings = new GlobalSettings();
        globalSettingsSection.Bind(settings);

        return settings;
    }
}
