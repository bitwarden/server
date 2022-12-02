using Microsoft.Extensions.Configuration;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest;

public class DatabaseTheoryAttribute : TheoryAttribute
{
    private static IConfiguration? _cachedConfiguration;

    public const string DapperSqlServerKey = "Dapper:SqlServer";
    public const string EfPostgresKey = "Ef:Postgres";
    public const string EfMySqlKey = "Ef:MySql";

    public DatabaseTheoryAttribute()
    {
        if (!HasAnyDatabaseSetup())
        {
            Skip = "No database connections strings setup.";
        }
    }

    private static bool HasAnyDatabaseSetup()
    {
        var config = GetConfiguration();
        return config.TryGetConnectionString(DapperSqlServerKey, out _) ||
          config.TryGetConnectionString(EfPostgresKey, out _) ||
          config.TryGetConnectionString(EfMySqlKey, out _);
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
