using AutoMapper;
using Bit.Core.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Recipes;
using Bit.Seeder.Services;
using Bit.SeederUtility.Configuration;
using CommandDotNet;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.SeederUtility.Commands;

[Command("organization", Description = "Seed an organization and organization users")]
public class OrganizationCommand
{
    [DefaultCommand]
    public void Execute(
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

        // Get a scoped DB context
        using var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var db = scopedServices.GetRequiredService<DatabaseContext>();

        var mapper = scopedServices.GetRequiredService<IMapper>();
        var passwordHasher = scopedServices.GetRequiredService<IPasswordHasher<User>>();
        var manglerService = scopedServices.GetRequiredService<IManglerService>();
        var recipe = new OrganizationWithUsersRecipe(db, mapper, passwordHasher, manglerService);
        recipe.Seed(name: name, domain: domain, users: users);
    }
}
