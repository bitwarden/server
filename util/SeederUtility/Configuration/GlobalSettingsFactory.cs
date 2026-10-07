using Bit.Core.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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
        // The generic host only reads DOTNET_ENVIRONMENT, while the rest of the repo keys off
        // ASPNETCORE_ENVIRONMENT; honor both. The seeder is a local development tool, so fall
        // back to Development rather than Production so appsettings.Development.json applies.
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                          ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                          ?? Environments.Development;

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            // Resolve appsettings files next to the binary, not the caller's working directory.
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = environment,
        });

        var settings = new GlobalSettings();
        builder.Configuration.GetSection("globalSettings").Bind(settings);

        return settings;
    }
}
