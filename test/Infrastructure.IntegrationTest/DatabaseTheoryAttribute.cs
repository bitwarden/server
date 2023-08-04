using Microsoft.Extensions.Configuration;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest;

public class DatabaseTheoryAttribute : TheoryAttribute
{
    private static IConfiguration? _cachedConfiguration;

    public DatabaseTheoryAttribute()
    {
        if (!HasAnyDatabaseSetup())
        {
            Skip = "No databases setup.";
        }
    }

    private static bool HasAnyDatabaseSetup()
    {
        var config = GetConfiguration();
        return config.GetDatabases().Length > 0;
    }

    public static IConfiguration GetConfiguration()
    {
        return _cachedConfiguration ??= new ConfigurationBuilder()
            .AddUserSecrets<DatabaseDataAttribute>(optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("BW_TEST_")
            .AddCommandLine(Environment.GetCommandLineArgs())
            .Build();
    }
}
