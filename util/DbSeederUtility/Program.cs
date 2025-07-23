using System.Threading.Tasks;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Seeder.Recipes;
using Bit.Seeder.Services;
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
        [Option('n', "Name", Description = "Name of organization")]
        string name,
        [Option('u', "users", Description = "Number of users to generate")]
        int users,
        [Option('d', "domain", Description = "Email domain for users")]
        string domain,
        [Option('p', "password", Description = "Default password for users")]
        string password = "Test123!@#"
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
        var cryptoService = scopedServices.GetRequiredService<ISeederCryptoService>();
        var passwordHasher = scopedServices.GetRequiredService<IPasswordHasher<User>>();

        var recipe = new OrganizationWithUsersRecipe(db, cryptoService, passwordHasher);
        recipe.Seed(name, users, domain, password);
    }

    [Command("org-complete", Description = "Seed an organization with users and vault items")]
    public async Task OrganizationComplete(
        [Option('n', "Name", Description = "Name of organization")]
        string name,
        [Option('d', "domain", Description = "Email domain for users")]
        string domain,
        [Option('u', "users", Description = "Number of users to generate")]
        int users = 10,
        [Option('p', "password", Description = "Default password for users")]
        string password = "Test123!@#",
        [Option("min-items", Description = "Minimum vault items per user")]
        int minItems = 3,
        [Option("max-items", Description = "Maximum vault items per user")]
        int maxItems = 5
    )
    {
        // Create service provider with necessary services
        var services = new ServiceCollection();
        ServiceCollectionExtension.ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        // Get a scoped DB context and services
        using var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var db = scopedServices.GetRequiredService<DatabaseContext>();
        var cryptoService = scopedServices.GetRequiredService<ISeederCryptoService>();
        var dataProtection = scopedServices.GetRequiredService<IDataProtectionService>();
        var passwordHasher = scopedServices.GetRequiredService<IPasswordHasher<User>>();

        var recipe = new OrganizationWithUsersAndVaultItemsRecipe(db);
        
        Console.WriteLine($"🚀 Creating organization '{name}' with {users} users and vault items...");
        
        var (org, userCount, totalItemCount) = await recipe.CreateOrganizationWithUsersAndItems(
            name, 
            domain, 
            password,
            cryptoService,
            dataProtection,
            passwordHasher,
            users,
            minItems,
            maxItems
        );
        
        Console.WriteLine($"✅ Successfully created:");
        Console.WriteLine($"   - Organization: {org.Name} (ID: {org.Id})");
        Console.WriteLine($"   - Users: {userCount}");
        Console.WriteLine($"   - Total vault items: {totalItemCount}");
    }
}
