using Bit.Seeder.Recipes;
using Bit.Seeder.Services;
using Bit.SeederUtility.Configuration;
using Bit.SeederUtility.Helpers;
using CommandDotNet;

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
        using var deps = SeederServiceFactory.Create(new SeederServiceOptions { EnableMangling = args.Mangle });
        var recipe = new OrganizationRecipe(deps.ToDependencies());

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

        ConsoleOutput.PrintMangleMap(deps);
    }

    private static void SeedIndividual(SeedArgs args)
    {
        using var deps = SeederServiceFactory.Create(new SeederServiceOptions { EnableMangling = args.Mangle });
        var recipe = new IndividualUserRecipe(deps.ToDependencies());

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

        ConsoleOutput.PrintMangleMap(deps);
    }

    private static void PrintAvailableSeeds()
    {
        var available = PresetCatalogService.ListAvailable();

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
        PresetCatalogService.IsIndividualPreset(presetName);
}
