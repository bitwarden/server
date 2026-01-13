using Bit.Seeder.Migration.Models;
using Microsoft.Extensions.Configuration;

namespace Bit.DbSeederUtility;

public static class MigrationSettingsFactory
{
    private static MigrationConfig? _migrationConfig;

    public static MigrationConfig MigrationConfig
    {
        get { return _migrationConfig ??= LoadMigrationConfig(); }
    }

    private static MigrationConfig LoadMigrationConfig()
    {
        Console.WriteLine("Loading migration configuration...");

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
            .AddUserSecrets("bitwarden-Api") // Load user secrets from the API project
            .AddEnvironmentVariables();

        var configuration = configBuilder.Build();
        var migrationSection = configuration.GetSection("migration");

        var config = new MigrationConfig();
        migrationSection.Bind(config);

        // Log configuration status
        Console.WriteLine($"Migration configuration loaded:");
        Console.WriteLine($"  Source DB: {(config.Source != null ? "Configured" : "Not configured")}");
        Console.WriteLine($"  Destinations: {config.Destinations.Count} configured");
        Console.WriteLine($"  CSV Output Dir: {config.CsvSettings.OutputDir}");
        Console.WriteLine($"  Excluded Tables: {config.ExcludeTables.Count}");

        return config;
    }
}
