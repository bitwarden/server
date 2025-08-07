using Bit.Core.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Recipes;
using CommandDotNet;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        string domain
    )
    {
        // Create service provider with necessary services
        var services = new ServiceCollection();
        ServiceCollectionExtension.ConfigureServices(services);
        services.TryAddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        var serviceProvider = services.BuildServiceProvider();

        // Get a scoped DB context
        using var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var db = scopedServices.GetRequiredService<DatabaseContext>();
        var passwordHasher = scopedServices.GetRequiredService<IPasswordHasher<User>>();

        var recipe = new OrganizationWithUsersRecipe(db, passwordHasher);
        recipe.Seed(name, users, domain);
    }
}
