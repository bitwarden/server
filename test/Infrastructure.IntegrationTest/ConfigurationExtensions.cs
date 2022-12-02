using Microsoft.Extensions.Configuration;

namespace Bit.Infrastructure.IntegrationTest;

public static class ConfigurationExtensions
{
    public static bool TryGetConnectionString(this IConfiguration config, string key, out string connectionString)
    {
        connectionString = config[key];
        if (string.IsNullOrEmpty(connectionString))
        {
            return false;
        }

        return true;
    }
}
