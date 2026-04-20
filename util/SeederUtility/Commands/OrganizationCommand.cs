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

            var result = ConsoleProgressReporter.RunWithProgress(
                deps.ToDependencies(),
                d => new OrganizationRecipe(d).Seed(args.ToOptions()));

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
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
