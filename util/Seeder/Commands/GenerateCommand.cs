using Bit.Seeder.Recipes;
using Bit.Seeder.Settings;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DatabaseContext = Bit.Infrastructure.EntityFramework.Repositories.DatabaseContext;

namespace Bit.Seeder.Commands;

public class GenerateCommand
{
    public bool Execute(string name, int users, string domain)
    {
        // Create service provider with necessary services
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        // TODO: Can we remove GenerateCommand and provide a RecipeFactory or something. Or wire up DI.
        using var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var db = scopedServices.GetRequiredService<DatabaseContext>();

        var recipe = new OrganizationWithUsersRecipe(db);
        recipe.Seed(name, users, domain);

        return true;
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

        services.AddDatabaseRepositories(globalSettings);
    }
}
