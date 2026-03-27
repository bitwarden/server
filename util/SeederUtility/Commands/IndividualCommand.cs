using Bit.Seeder.Recipes;
using Bit.SeederUtility.Configuration;
using Bit.SeederUtility.Helpers;
using CommandDotNet;

namespace Bit.SeederUtility.Commands;

[Command("individual", Description = "Seed a standalone individual user with optional personal vault data")]
public class IndividualCommand
{
    [DefaultCommand]
    public void Execute(IndividualArgs args)
    {
        try
        {
            args.Validate();

            using var deps = SeederServiceFactory.Create(new SeederServiceOptions { EnableMangling = args.Mangle });
            var recipe = new IndividualUserRecipe(deps.ToDependencies());

            var result = recipe.Seed(args.ToOptions());

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
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
