using Bit.Core.Settings;
using Microsoft.Extensions.Configuration;

namespace Bit.DbSeederUtility;

public static class GlobalSettingsFactory
{
    private static GlobalSettings? _globalSettings;

    public static GlobalSettings GlobalSettings
    {
        get { return _globalSettings ??= LoadGlobalSettings(); }
    }

    private static GlobalSettings LoadGlobalSettings()
    {
        Console.WriteLine("Loading global settings...");

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
            .AddUserSecrets("bitwarden-Api") // Load user secrets from the API project
            .AddEnvironmentVariables();

        var configuration = configBuilder.Build();
        var globalSettingsSection = configuration.GetSection("globalSettings");

        var settings = new GlobalSettings();
        globalSettingsSection.Bind(settings);

        return settings;
    }
}
