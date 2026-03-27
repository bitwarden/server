using Bit.Seeder.Recipes;
using Bit.SeederUtility.Configuration;
using Bit.SeederUtility.Helpers;
using CommandDotNet;

namespace Bit.SeederUtility.Commands;

[Command("organization", Description = "Seed an organization with users and optional vault data (ciphers, collections, groups)")]
public class OrganizationCommand
{
    [DefaultCommand]
    public void Execute(OrganizationArgs args)
    {
        try
        {
            args.Validate();

            using var deps = SeederServiceFactory.Create(new SeederServiceOptions { EnableMangling = args.Mangle });
            var recipe = new OrganizationRecipe(deps.ToDependencies());

            var result = recipe.Seed(args.ToOptions());

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
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
