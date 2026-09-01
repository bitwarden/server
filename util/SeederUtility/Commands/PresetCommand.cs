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
    public async Task ExecuteAsync(PresetArgs args)
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
                if (args.StripeBilling)
                {
                    throw new ArgumentException(
                        $"--stripe-billing is not supported for individual preset '{args.Name}'. " +
                        "Only organization presets can be billed today; premium billing is a separate task.");
                }

                await RunIndividualPresetAsync(args);
            }
            else
            {
                await RunOrganizationPresetAsync(args);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task RunOrganizationPresetAsync(PresetArgs args)
    {
        using var deps = SeederServiceFactory.Create(new SeederServiceOptions { EnableMangling = args.Mangle });

        await Console.Error.WriteLineAsync($"Seeding organization from preset '{args.Name}'...");
        var result = await ConsoleProgressReporter.RunWithProgressAsync(
            deps.ToDependencies(),
            d => new OrganizationRecipe(d).SeedAsync(
                args.Name!,
                args.Password,
                args.KdfIterations,
                args.OrgName,
                args.OwnerEmail,
                stripeBilling: args.ToStripeBillingOptions()));

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

        if (args.StripeBilling)
        {
            ConsoleOutput.PrintRow("StripeCustomer", result.GatewayCustomerId);
            ConsoleOutput.PrintRow("StripeSubscription", result.GatewaySubscriptionId);
        }

        ConsoleOutput.PrintMangleMap(deps);

        if (result.SsoIdentifier is not null)
        {
            ConsoleOutput.PrintSsoWiring(result.OrganizationId, result.SsoIdentifier, result.OwnerEmail);
        }
    }

    private static async Task RunIndividualPresetAsync(PresetArgs args)
    {
        using var deps = SeederServiceFactory.Create(new SeederServiceOptions { EnableMangling = args.Mangle });

        await Console.Error.WriteLineAsync($"Seeding individual user from preset '{args.Name}'...");
        var result = await ConsoleProgressReporter.RunWithProgressAsync(
            deps.ToDependencies(),
            d => new IndividualUserRecipe(d).SeedAsync(args.Name!, args.Password, args.KdfIterations));

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
