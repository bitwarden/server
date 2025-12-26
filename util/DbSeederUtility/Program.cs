using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Recipes;
using CommandDotNet;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.DbSeederUtility;

public class Program
{
    private static int Main(string[] args)
    {
        return new AppRunner<Program>()
            .Run(args);
    }

    [Command("organization", Description = "Seed an organization and organization users")]
    public void Organization(
        [Option('N', "Name", Description = "Name of organization")]
        string name,
        [Option('U', "users", Description = "Number of users to generate")]
        int users,
        [Option('D', "domain", Description = "Email domain for users")]
        string domain,
        [Option('C', "collections", Description = "Number of collections to generate")]
        int collections = 0,
        [Option('I', "ciphers-per-collection", Description = "Number of ciphers per collection")]
        int ciphersPerCollection = 0
    )
    {
        // Create service provider with necessary services
        var services = new ServiceCollection();
        ServiceCollectionExtension.ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        // Get a scoped DB context
        using var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var db = scopedServices.GetRequiredService<DatabaseContext>();
        var passwordHasher = scopedServices.GetRequiredService<IPasswordHasher<Bit.Core.Entities.User>>();

        var recipe = new OrganizationWithUsersRecipe(db, passwordHasher);
        recipe.Seed(name: name, domain: domain, users: users, collections: collections, ciphersPerCollection: ciphersPerCollection);
    }
}
