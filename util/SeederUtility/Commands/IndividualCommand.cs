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
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
