using Bit.Core.Settings;
using Microsoft.Extensions.Configuration;

namespace Bit.Core.Test.Helpers.Factories;

public static class GlobalSettingsFactory
{
    public static GlobalSettings GlobalSettings { get; } = new();

    static GlobalSettingsFactory()
    {
        var configBuilder = new ConfigurationBuilder().AddUserSecrets("bitwarden-Api");
        var Configuration = configBuilder.Build();
        ConfigurationBinder.Bind(Configuration.GetSection("GlobalSettings"), GlobalSettings);
    }
}
