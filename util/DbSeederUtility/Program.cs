using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Recipes;
using CommandDotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.DbSeederUtility;

public class Program
{
    private static int Main(string[] args)
    {
        // Ensure global settings are loaded
        var globalSettings = GlobalSettingsFactory.GlobalSettings;

        return new AppRunner<Program>()
            .Run(args);
    }

    [Command("organization", Description = "Seed an organization and organization users")]
    public void Organization(
        [Option('n', "Name", Description = "Name of organization")]
        string name,
        [Option('u', "users", Description = "Number of users to generate")]
        int users,
        [Option('d', "domain", Description = "Email domain for users")]
        string domain
    )
    {
        // Create service provider with necessary services
        var services = new ServiceCollection();
        ServiceCollectionExtension.ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        // TODO: Can we remove GenerateCommand and provide a RecipeFactory or something. Or wire up DI.
        using var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var db = scopedServices.GetRequiredService<DatabaseContext>();

        var recipe = new OrganizationWithUsersRecipe(db);
        recipe.Seed(name, users, domain);
    }
}
