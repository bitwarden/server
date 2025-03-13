using Bit.Core.Enums;
using Bit.Seeder.Services;
using Bit.Seeder.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Commands;

public class ExtractCommand
{
    public bool Execute(string seedName)
    {
        try
        {
            // Create service provider with necessary services
            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            // Get the necessary services
            var seederService = serviceProvider.GetRequiredService<ISeederService>();
            var logger = serviceProvider.GetRequiredService<ILogger<ExtractCommand>>();

            logger.LogInformation($"Extracting data from database to seed: {seedName}");

            // Execute the extract operation
            seederService.ExtractSeedsAsync(seedName).GetAwaiter().GetResult();

            logger.LogInformation("Seed extraction completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting seeds: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    private void ConfigureServices(ServiceCollection services)
    {
        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Add JSON file configuration for other app settings not in GlobalSettings
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets("Bit.Seeder")
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Get global settings
        var globalSettings = GlobalSettingsFactory.GlobalSettings;
        services.AddSingleton(globalSettings);

        // Configure database provider
        var provider = globalSettings.DatabaseProvider.ToLowerInvariant() switch
        {
            "postgres" or "postgresql" => SupportedDatabaseProviders.Postgres,
            "mysql" or "mariadb" => SupportedDatabaseProviders.MySql,
            "sqlite" => SupportedDatabaseProviders.Sqlite,
            _ => SupportedDatabaseProviders.SqlServer
        };

        var connectionString = provider switch
        {
            SupportedDatabaseProviders.Postgres => globalSettings.PostgreSql?.ConnectionString,
            SupportedDatabaseProviders.MySql => globalSettings.MySql?.ConnectionString,
            SupportedDatabaseProviders.Sqlite => globalSettings.Sqlite?.ConnectionString,
            _ => globalSettings.SqlServer?.ConnectionString
        };

        // Register database context
        services.AddDbContext<DatabaseContext>(options =>
        {
            switch (provider)
            {
                case SupportedDatabaseProviders.Postgres:
                    options.UseNpgsql(connectionString);
                    break;
                case SupportedDatabaseProviders.MySql:
                    options.UseMySql(connectionString!, ServerVersion.AutoDetect(connectionString!));
                    break;
                case SupportedDatabaseProviders.Sqlite:
                    options.UseSqlite(connectionString!);
                    break;
                default:
                    options.UseSqlServer(connectionString!);
                    break;
            }
        });

        // Add Data Protection services
        services.AddDataProtection()
            .SetApplicationName("Bitwarden");

        // Register other services
        services.AddTransient<ISeederService, SeederService>();
        services.AddTransient<IDatabaseService, DatabaseService>();
        services.AddTransient<IEncryptionService, EncryptionService>();
    }
}
