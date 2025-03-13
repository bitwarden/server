using Microsoft.Extensions.Configuration;

namespace Bit.Seeder.Settings;

public static class GlobalSettingsFactory
{
    private static GlobalSettings? _globalSettings;

    public static GlobalSettings GlobalSettings
    {
        get
        {
            if (_globalSettings == null)
            {
                _globalSettings = LoadGlobalSettings();
            }
            
            return _globalSettings;
        }
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
        
        // Debug: Print all settings from globalSettings section
        foreach (var setting in globalSettingsSection.GetChildren())
        {
            Console.WriteLine($"Found setting: {setting.Key}");
            foreach (var child in setting.GetChildren())
            {
                Console.WriteLine($"  - {setting.Key}.{child.Key}");
            }
        }
        
        var settings = new GlobalSettings();
        globalSettingsSection.Bind(settings);
        
        // Output the loaded settings
        Console.WriteLine($"Loaded DatabaseProvider: {settings.DatabaseProvider}");
        Console.WriteLine($"PostgreSql settings loaded: {settings.PostgreSql != null}");
        Console.WriteLine($"SqlServer settings loaded: {settings.SqlServer != null}");
        Console.WriteLine($"MySql settings loaded: {settings.MySql != null}");
        Console.WriteLine($"Sqlite settings loaded: {settings.Sqlite != null}");
        
        // Check for case sensitivity issue with PostgreSql/postgresql keys
        var postgresqlValue = globalSettingsSection.GetSection("postgresql")?.Value;
        var postgreSqlValue = globalSettingsSection.GetSection("postgreSql")?.Value;
        Console.WriteLine($"Raw check - postgresql setting exists: {postgresqlValue != null}");
        Console.WriteLine($"Raw check - postgreSql setting exists: {postgreSqlValue != null}");
        
        return settings;
    }
}

// Non-static version that can accept command-line arguments
public class GlobalSettingsFactoryWithArgs
{
    public GlobalSettings GlobalSettings { get; }

    public GlobalSettingsFactoryWithArgs(string[] args)
    {
        GlobalSettings = new GlobalSettings();
        
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
            .AddUserSecrets("bitwarden-Api")
            .AddCommandLine(args)
            .AddEnvironmentVariables()
            .Build();

        config.GetSection("globalSettings").Bind(GlobalSettings);
    }
} 