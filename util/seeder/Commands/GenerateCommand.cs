using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Bit.Seeder.Services;
using Bit.Seeder.Settings;
using Microsoft.AspNetCore.DataProtection;
using Bit.Core;

namespace Bit.Seeder.Commands
{
    public class GenerateCommand
    {
        public bool Execute(int users, int ciphersPerUser, string seedName, bool loadImmediately = false)
        {
            try
            {
                // Create service provider with necessary services
                var services = new ServiceCollection();
                ConfigureServices(services);
                var serviceProvider = services.BuildServiceProvider();

                // Get the seeder service
                var seederService = serviceProvider.GetRequiredService<ISeederService>();
                var logger = serviceProvider.GetRequiredService<ILogger<GenerateCommand>>();

                logger.LogInformation($"Generating seeds for {users} users with {ciphersPerUser} ciphers per user");
                logger.LogInformation($"Seed name: {seedName}");

                // Execute the appropriate action based on whether we need to load immediately
                if (loadImmediately)
                {
                    // Generate and load directly without creating intermediate files
                    seederService.GenerateAndLoadSeedsAsync(users, ciphersPerUser, seedName).GetAwaiter().GetResult();
                    logger.LogInformation("Seeds generated and loaded directly to database");
                }
                else
                {
                    // Only generate seeds and save to files
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    seederService.GenerateSeedsAsync(users, ciphersPerUser, seedName).GetAwaiter().GetResult();
                    logger.LogInformation("Seed generation completed successfully");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating seeds: {ex.Message}");
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
} 