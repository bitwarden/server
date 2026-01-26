using Bit.Seeder.Data.Enums;
using Bit.Seeder.Recipes;
using CommandDotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.DbSeederUtility;

public class Program
{
    private static int Main(string[] args)
    {
        return new AppRunner<Program>()
            .Run(args);
    }

    [Command("organization", Description = "Seed an organization with users and optional ciphers")]
    public void Organization(
        [Option('n', "name", Description = "Name of organization")]
        string name,
        [Option('u', "users", Description = "Number of users to generate")]
        int users,
        [Option('d', "domain", Description = "Email domain for users")]
        string domain,
        [Option('c', "ciphers", Description = "Number of login ciphers to create")]
        int ciphers = 0,
        [Option('s', "structure", Description = "Org structure for collections: Traditional, Spotify, or Modern")]
        string? structure = null
    )
    {
        var structureModel = ParseStructureModel(structure);

        var services = new ServiceCollection();
        ServiceCollectionExtension.ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        OrganizationWithUsersRecipe.SeedFromServices(scope.ServiceProvider, name, domain, users, ciphers,
            structureModel: structureModel);
    }

    private static OrgStructureModel? ParseStructureModel(string? structure)
    {
        if (string.IsNullOrEmpty(structure))
        {
            return null;
        }

        return structure.ToLowerInvariant() switch
        {
            "traditional" => OrgStructureModel.Traditional,
            "spotify" => OrgStructureModel.Spotify,
            "modern" => OrgStructureModel.Modern,
            _ => throw new ArgumentException($"Unknown structure '{structure}'. Use: Traditional, Spotify, or Modern")
        };
    }
}
