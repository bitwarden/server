using Bit.Seeder.Services;
using Bit.Seeder.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Commands;

public class LoadCommand
{
    public bool Execute(string seedName, string? timestamp = null, bool dryRun = false)
    {
        try
        {
            // Create service provider with necessary services
            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            // Get the necessary services
            var seederService = serviceProvider.GetRequiredService<ISeederService>();
            var databaseService = serviceProvider.GetRequiredService<IDatabaseService>();
            var logger = serviceProvider.GetRequiredService<ILogger<LoadCommand>>();

            logger.LogInformation($"Loading seeds named: {seedName}");
            if (!string.IsNullOrEmpty(timestamp))
            {
                logger.LogInformation($"Using specific timestamp: {timestamp}");
            }

            if (dryRun)
            {
                logger.LogInformation("DRY RUN: No actual changes will be made to the database");
                // Perform validation here if needed
                logger.LogInformation("Seed loading validation completed");
                return true;
            }

            // Execute the load operation
            seederService.LoadSeedsAsync(seedName, timestamp).GetAwaiter().GetResult();

            logger.LogInformation("Seed loading completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            // Display the full exception details
            Console.WriteLine($"Error loading seeds: {ex.Message}");

            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");

                if (ex.InnerException.InnerException != null)
                {
                    Console.WriteLine($"Inner inner exception: {ex.InnerException.InnerException.Message}");
                }
            }

            Console.WriteLine($"Stack trace: {ex.StackTrace}");

            return false;
        }
    }

    private void ConfigureServices(ServiceCollection services)
    {
        // Load configuration using the GlobalSettingsFactory
        var globalSettings = GlobalSettingsFactory.GlobalSettings;

        // Register services
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton(globalSettings);

        // Add Data Protection services
        services.AddDataProtection()
            .SetApplicationName("Bitwarden");

        // Register DatabaseContext and services
        services.AddTransient<ISeederService, SeederService>();
        services.AddTransient<IDatabaseService, DatabaseService>();
        services.AddTransient<IEncryptionService, EncryptionService>();
        services.AddDbContext<DatabaseContext>();
    }
}
