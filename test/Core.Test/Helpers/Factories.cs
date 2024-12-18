using Bit.Core.Settings;
using Microsoft.Extensions.Configuration;

namespace Bit.Core.Test.Helpers.Factories;

public static class GlobalSettingsFactory
{
    private static readonly IConfiguration _configuration;

    static GlobalSettingsFactory()
    {
        var configBuilder = new ConfigurationBuilder().AddUserSecrets("bitwarden-Api");
        _configuration = configBuilder.Build();
    }

    public static GlobalSettings Create()
    {
        var newGlobalSettings = new GlobalSettings();
        _configuration.GetSection("GlobalSettings").Bind(newGlobalSettings);
        return newGlobalSettings;
    }
}
