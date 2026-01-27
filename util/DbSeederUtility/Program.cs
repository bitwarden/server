using AutoMapper;
using Bit.Core.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Options;
using Bit.Seeder.Recipes;
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

        var recipe = new OrganizationWithUsersRecipe(db);
        recipe.Seed(name: name, domain: domain, users: users);
    }

    [Command("vault-organization", Description = "Seed an organization with users and encrypted vault data (ciphers, collections, groups)")]
    public void VaultOrganization(
        [Option('n', "name", Description = "Name of organization")]
        string name,
        [Option('u', "users", Description = "Number of users to generate (minimum 1)")]
        int users,
        [Option('d', "domain", Description = "Email domain for users")]
        string domain,
        [Option('c', "ciphers", Description = "Number of login ciphers to create (required, minimum 1)")]
        int ciphers,
        [Option('g', "groups", Description = "Number of groups to create (required, minimum 1)")]
        int groups,
        [Option('m', "mix-user-statuses", Description = "Use realistic status mix (85% confirmed, 5% each invited/accepted/revoked). Requires >= 10 users.")]
        bool mixStatuses = true,
        [Option('o', "org-structure", Description = "Org structure for collections: Traditional, Spotify, or Modern")]
        string? structure = null
    )
    {
        if (users < 1)
        {
            throw new ArgumentException("Users must be at least 1. Use another command for orgs without users.");
        }

        if (ciphers < 1)
        {
            throw new ArgumentException("Ciphers must be at least 1. Use another command for orgs without vault data.");
        }

        if (groups < 1)
        {
            throw new ArgumentException("Groups must be at least 1. Use another command for orgs without groups.");
        }

        var structureModel = ParseOrgStructure(structure);

        var services = new ServiceCollection();
        ServiceCollectionExtension.ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;

        var recipe = new OrganizationWithVaultRecipe(
            scopedServices.GetRequiredService<DatabaseContext>(),
            scopedServices.GetRequiredService<IMapper>(),
            scopedServices.GetRequiredService<RustSdkService>(),
            scopedServices.GetRequiredService<IPasswordHasher<User>>());

        recipe.Seed(new OrganizationVaultOptions
        {
            Name = name,
            Domain = domain,
            Users = users,
            Ciphers = ciphers,
            Groups = groups,
            RealisticStatusMix = mixStatuses,
            StructureModel = structureModel
        });
    }

    private static OrgStructureModel? ParseOrgStructure(string? structure)
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
