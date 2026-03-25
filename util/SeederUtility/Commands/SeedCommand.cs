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

[Command("seed", Description = "Seed database using fixture-based presets")]
public class SeedCommand
{
    [DefaultCommand]
    public void Execute(SeedArgs args)
    {
        try
        {
            args.Validate();

            if (args.List)
            {
                PrintAvailableSeeds();
                return;
            }

            if (IsIndividualPreset(args.Preset!))
            {
                SeedIndividual(args);
            }
            else
            {
                SeedOrganization(args);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void SeedOrganization(SeedArgs args)
    {
        var services = new ServiceCollection();
        ServiceCollectionExtension.ConfigureServices(services, enableMangling: args.Mangle);
        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;

        var db = scopedServices.GetRequiredService<DatabaseContext>();
        var mapper = scopedServices.GetRequiredService<IMapper>();
        var passwordHasher = scopedServices.GetRequiredService<IPasswordHasher<User>>();
        var manglerService = scopedServices.GetRequiredService<IManglerService>();

        var recipe = new OrganizationRecipe(db, mapper, passwordHasher, manglerService);

        Console.WriteLine($"Seeding organization from preset '{args.Preset}'...");
        var result = recipe.Seed(args.Preset!, args.Password, args.KdfIterations);

        Console.WriteLine($"Created organization (ID: {result.OrganizationId})");

        if (result.OwnerEmail is not null)
        {
            Console.WriteLine($"Owner: {result.OwnerEmail}");
        }

        if (result.UsersCount > 0)
        {
            Console.WriteLine($"Created {result.UsersCount} users");
        }

        if (result.GroupsCount > 0)
        {
            Console.WriteLine($"Created {result.GroupsCount} groups");
        }

        if (result.CollectionsCount > 0)
        {
            Console.WriteLine($"Created {result.CollectionsCount} collections");
        }

        if (result.CiphersCount > 0)
        {
            Console.WriteLine($"Created {result.CiphersCount} ciphers");
        }
    }

    private static void SeedIndividual(SeedArgs args)
    {
        var services = new ServiceCollection();
        ServiceCollectionExtension.ConfigureServices(services, enableMangling: args.Mangle);
        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;

        var recipe = new IndividualUserRecipe(
            scopedServices.GetRequiredService<DatabaseContext>(),
            scopedServices.GetRequiredService<IMapper>(),
            scopedServices.GetRequiredService<IPasswordHasher<User>>(),
            scopedServices.GetRequiredService<IManglerService>());

        Console.WriteLine($"Seeding individual user from preset '{args.Preset}'...");
        var result = recipe.Seed(args.Preset!, args.Password, args.KdfIterations);

        Console.WriteLine($"Created user (ID: {result.UserId})");

        if (result.Email is not null)
        {
            Console.WriteLine($"Email: {result.Email}");
        }

        Console.WriteLine($"Premium: {result.Premium}");

        if (result.FoldersCount > 0)
        {
            Console.WriteLine($"Created {result.FoldersCount} folders");
        }

        if (result.CiphersCount > 0)
        {
            Console.WriteLine($"Created {result.CiphersCount} ciphers");
        }
    }

    private static void PrintAvailableSeeds()
    {
        var available = OrganizationRecipe.ListAvailable();

        var orgPresets = new List<string>();
        var individualPresets = new List<string>();

        foreach (var presetName in available.Presets)
        {
            if (IsIndividualPreset(presetName))
            {
                individualPresets.Add(presetName);
            }
            else
            {
                orgPresets.Add(presetName);
            }
        }

        Console.WriteLine("Organization Presets:");
        foreach (var preset in orgPresets)
        {
            Console.WriteLine($"  - {preset}");
        }
        Console.WriteLine();

        Console.WriteLine("Individual User Presets:");
        foreach (var preset in individualPresets)
        {
            Console.WriteLine($"  - {preset}");
        }
        Console.WriteLine();

        Console.WriteLine("Available Fixtures:");
        foreach (var (category, fixtures) in available.Fixtures.OrderBy(kvp => kvp.Key))
        {
            // Guard: Skip empty or single-character categories to prevent IndexOutOfRangeException
            if (string.IsNullOrEmpty(category) || category.Length < 2)
            {
                continue;
            }

            var categoryName = char.ToUpperInvariant(category[0]) + category[1..];
            Console.WriteLine($"  {categoryName}:");
            foreach (var fixture in fixtures)
            {
                Console.WriteLine($"    - {fixture}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Use: SeederUtility seed --preset <name>");
    }

    private static bool IsIndividualPreset(string presetName) =>
        presetName.StartsWith("individual.", StringComparison.OrdinalIgnoreCase);
}
