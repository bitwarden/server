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

[Command("organization", Description = "Seed an organization with users and optional vault data (ciphers, collections, groups)")]
public class OrganizationCommand
{
    [DefaultCommand]
    public void Execute(OrganizationArgs args)
    {
        args.Validate();

        var services = new ServiceCollection();
        ServiceCollectionExtension.ConfigureServices(services, enableMangling: args.Mangle);
        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;

        var manglerService = scopedServices.GetRequiredService<IManglerService>();
        var recipe = new OrganizationRecipe(
            scopedServices.GetRequiredService<DatabaseContext>(),
            scopedServices.GetRequiredService<IMapper>(),
            scopedServices.GetRequiredService<IPasswordHasher<User>>(),
            manglerService);

        var result = recipe.Seed(args.ToOptions());

        Console.WriteLine($"✓ Created organization (ID: {result.OrganizationId})");
        if (result.OwnerEmail is not null)
        {
            Console.WriteLine($"✓ Owner: {result.OwnerEmail}");
        }
        if (result.UsersCount > 0)
        {
            Console.WriteLine($"✓ Created {result.UsersCount} users");
        }
        if (result.GroupsCount > 0)
        {
            Console.WriteLine($"✓ Created {result.GroupsCount} groups");
        }
        if (result.CollectionsCount > 0)
        {
            Console.WriteLine($"✓ Created {result.CollectionsCount} collections");
        }
        if (result.CiphersCount > 0)
        {
            Console.WriteLine($"✓ Created {result.CiphersCount} ciphers");
        }

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
