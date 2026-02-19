using System.Diagnostics;
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

            // Handle list mode - no database needed
            if (args.List)
            {
                var available = OrganizationFromPresetRecipe.ListAvailable();
                PrintAvailableSeeds(available);
                return;
            }

            // Create service provider - same pattern as other commands
            var services = new ServiceCollection();
            ServiceCollectionExtension.ConfigureServices(services, enableMangling: args.Mangle);
            var serviceProvider = services.BuildServiceProvider();

            using var scope = serviceProvider.CreateScope();
            var scopedServices = scope.ServiceProvider;

            var db = scopedServices.GetRequiredService<DatabaseContext>();
            var mapper = scopedServices.GetRequiredService<IMapper>();
            var passwordHasher = scopedServices.GetRequiredService<IPasswordHasher<User>>();
            var manglerService = scopedServices.GetRequiredService<IManglerService>();

            // Create recipe - CLI is "dumb", recipe handles complexity
            var recipe = new OrganizationFromPresetRecipe(db, mapper, passwordHasher, manglerService);

            var stopwatch = Stopwatch.StartNew();

            Console.WriteLine($"Seeding organization from preset '{args.Preset}'...");
            var result = recipe.Seed(args.Preset!, args.Password);

            stopwatch.Stop();
            PrintSeedResult(result, stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void PrintAvailableSeeds(AvailableSeeds available)
    {
        Console.WriteLine("Available Presets:");
        foreach (var preset in available.Presets)
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

    private static void PrintSeedResult(SeedResult result, TimeSpan elapsed)
    {
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

        Console.WriteLine($"Done in {elapsed.TotalSeconds:F1}s");
    }
}
