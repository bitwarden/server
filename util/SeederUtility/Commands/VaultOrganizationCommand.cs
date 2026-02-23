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

[Command("vault-organization", Description = "Seed an organization with users and encrypted vault data (ciphers, collections, groups)")]
public class VaultOrganizationCommand
{
    [DefaultCommand]
    public void Execute(VaultOrganizationArgs args)
    {
        args.Validate();

        var services = new ServiceCollection();
        ServiceCollectionExtension.ConfigureServices(services, enableMangling: args.Mangle);
        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;

        var manglerService = scopedServices.GetRequiredService<IManglerService>();
        var recipe = new OrganizationWithVaultRecipe(
            scopedServices.GetRequiredService<DatabaseContext>(),
            scopedServices.GetRequiredService<IMapper>(),
            scopedServices.GetRequiredService<IPasswordHasher<User>>(),
            manglerService);

        recipe.Seed(args.ToOptions());

        if (!manglerService.IsEnabled)
        {
            return;
        }

        var map = manglerService.GetMangleMap();
        Console.WriteLine("--- Mangled Data Map ---");
        foreach (var (original, mangled) in map)
        {
            Console.WriteLine($"{original} -> {mangled}");
        }
    }
}
