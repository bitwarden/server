using System.Text.Json;
using Bit.Seeder.Recipes;
using Bit.Seeder.Services;
using Bit.SeederUtility.Configuration;
using Bit.SeederUtility.Helpers;
using CommandDotNet;

namespace Bit.SeederUtility.Commands;

[Command("preset", Description = "Seed database using a named preset")]
public class PresetCommand
{
    [DefaultCommand]
    public void Execute(PresetArgs args)
    {
        try
        {
            args.Validate();

            if (args.List)
            {
                PrintAvailablePresets(args.GetOutputFormat());
                return;
            }

            if (IsIndividualPreset(args.Name!))
            {
                RunIndividualPreset(args);
            }
            else
            {
                RunOrganizationPreset(args);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void RunOrganizationPreset(PresetArgs args)
    {
        using var deps = SeederServiceFactory.Create(new SeederServiceOptions { EnableMangling = args.Mangle });
        var recipe = new OrganizationRecipe(deps.ToDependencies());

        Console.WriteLine($"Seeding organization from preset '{args.Name}'...");
        var result = recipe.Seed(args.Name!, args.Password, args.KdfIterations);

        ConsoleOutput.PrintRow("Organization", result.OrganizationId);
        if (result.OwnerEmail is not null)
        {
            ConsoleOutput.PrintRow("Owner", result.OwnerEmail);
        }
        ConsoleOutput.PrintRow("Password", result.Password);
        if (result.ApiKey is not null)
        {
            ConsoleOutput.PrintRow("ApiKey", result.ApiKey);
        }
        ConsoleOutput.PrintCountRow("Users", result.UsersCount);
        ConsoleOutput.PrintCountRow("Groups", result.GroupsCount);
        ConsoleOutput.PrintCountRow("Collections", result.CollectionsCount);
        ConsoleOutput.PrintCountRow("Ciphers", result.CiphersCount);

        ConsoleOutput.PrintMangleMap(deps);
    }

    private static void RunIndividualPreset(PresetArgs args)
    {
        using var deps = SeederServiceFactory.Create(new SeederServiceOptions { EnableMangling = args.Mangle });
        var recipe = new IndividualUserRecipe(deps.ToDependencies());

        Console.WriteLine($"Seeding individual user from preset '{args.Name}'...");
        var result = recipe.Seed(args.Name!, args.Password, args.KdfIterations);

        ConsoleOutput.PrintRow("User", result.UserId);
        if (result.Email is not null)
        {
            ConsoleOutput.PrintRow("Email", result.Email);
        }
        ConsoleOutput.PrintRow("Password", result.Password);
        ConsoleOutput.PrintRow("Premium", result.Premium);
        if (result.ApiKey is not null)
        {
            ConsoleOutput.PrintRow("ApiKey", result.ApiKey);
        }
        ConsoleOutput.PrintCountRow("Folders", result.FoldersCount);
        ConsoleOutput.PrintCountRow("Ciphers", result.CiphersCount);

        ConsoleOutput.PrintMangleMap(deps);
    }

    private static void PrintAvailablePresets(OutputFormat format = OutputFormat.Text)
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

        if (format == OutputFormat.Json)
        {
            var output = new
            {
                organization = orgPresets,
                individual = individualPresets,
            };
            Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
            return;
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
        Console.WriteLine("Use: SeederUtility preset --name <name>");
    }

    private static bool IsIndividualPreset(string presetName) =>
        PresetCatalogService.IsIndividualPreset(presetName);
}
