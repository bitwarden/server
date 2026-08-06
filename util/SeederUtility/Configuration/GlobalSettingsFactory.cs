using System.Reflection;
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
        var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                        ?? Directory.GetCurrentDirectory();

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile(
                $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
                optional: true, reloadOnChange: true)
            .AddUserSecrets("bitwarden-seeder-utility")
            .AddEnvironmentVariables();

        var configuration = configBuilder.Build();
        var globalSettingsSection = configuration.GetSection("globalSettings");

        var settings = new GlobalSettings();
        globalSettingsSection.Bind(settings);

        return settings;
    }
}
