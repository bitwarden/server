using System.Diagnostics;
using AutoMapper;
using Bit.Core.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
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

    [Command("vault-organization", Description = "Seed an organization with users and encrypted vault data (ciphers, collections, groups)")]
    public void VaultOrganization(VaultOrganizationArgs args)
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

    [Command("seed", Description = "Seed database using fixture-based presets")]
    public void Seed(SeedArgs args)
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

        try
        {
            var stopwatch = Stopwatch.StartNew();

            Console.WriteLine($"Seeding organization from preset '{args.Preset}'...");
            var result = recipe.Seed(args.Preset!);

            stopwatch.Stop();
            PrintSeedResult(result, stopwatch.Elapsed);
        }
        catch (InvalidOperationException ex)
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
        Console.WriteLine("Use: DbSeeder.exe seed --preset <name>");
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
