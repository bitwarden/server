using Microsoft.Extensions.Configuration;

namespace Bit.Core.Utilities;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddKeyPerFile(this IConfigurationBuilder configurationBuilder, IConfiguration configuration)
    {
        var fileConfigDirectory = configuration["globalSettings:installation:fileConfigDirectory"];
        if (!string.IsNullOrWhiteSpace(fileConfigDirectory) && Directory.Exists(fileConfigDirectory))
        {
            configurationBuilder.AddKeyPerFile(directoryPath: fileConfigDirectory, optional: false, reloadOnChange: true);
        }

        return configurationBuilder;
    }
}
