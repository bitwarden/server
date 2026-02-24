using Bit.Seeder.Data.Enums;
using Bit.Seeder.Factories;
using Bit.Seeder.Options;
using CommandDotNet;

namespace Bit.SeederUtility.Commands;

/// <summary>
/// CLI argument model for the organization command.
/// Maps to <see cref="OrganizationVaultOptions"/> for the Seeder library.
/// </summary>
public class OrganizationArgs : IArgumentModel
{
    [Option('n', "name", Description = "Name of organization")]
    public string Name { get; set; } = null!;

    [Option('u', "users", Description = "Number of users to generate (minimum 1)")]
    public int Users { get; set; }

    [Option('d', "domain", Description = "Email domain for users")]
    public string Domain { get; set; } = null!;

    [Option('c', "ciphers", Description = "Number of ciphers to create (default: 0, no vault data)")]
    public int? Ciphers { get; set; }

    [Option('g', "groups", Description = "Number of groups to create (default: 0, no groups)")]
    public int? Groups { get; set; }

    [Option('m', "mix-user-statuses", Description = "Use realistic status mix (85% confirmed, 5% each invited/accepted/revoked). Requires >= 10 users.")]
    public bool MixStatuses { get; set; } = true;

    [Option('o', "org-structure", Description = "Org structure for collections: Traditional, Spotify, or Modern")]
    public string? Structure { get; set; }

    [Option('r', "region", Description = "Geographic region for names: NorthAmerica, Europe, AsiaPacific, LatinAmerica, MiddleEast, Africa, or Global")]
    public string? Region { get; set; }

    [Option("mangle", Description = "Enable mangling for test isolation")]
    public bool Mangle { get; set; } = false;

    [Option("password", Description = "Password for all seeded accounts (default: asdfasdfasdf)")]
    public string? Password { get; set; }

    [Option("plan-type", Description = "Billing plan type: free, teams-monthly, teams-annually, enterprise-monthly, enterprise-annually, teams-starter, families-annually. Defaults to enterprise-annually.")]
    public string PlanType { get; set; } = "enterprise-annually";

    public void Validate()
    {
        if (Users < 1)
        {
            throw new ArgumentException("Users must be at least 1.");
        }

        if (!Domain.EndsWith(".example", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Domain must end with '.example' (RFC 2606). Example: myorg.example");
        }

        if (!string.IsNullOrEmpty(Structure))
        {
            ParseOrgStructure(Structure);
        }

        if (!string.IsNullOrEmpty(Region))
        {
            ParseGeographicRegion(Region);
        }

        PlanFeatures.Parse(PlanType);
    }

    public OrganizationVaultOptions ToOptions() => new()
    {
        Name = Name,
        Domain = Domain,
        Users = Users,
        Ciphers = Ciphers ?? 0,
        Groups = Groups ?? 0,
        RealisticStatusMix = MixStatuses,
        StructureModel = ParseOrgStructure(Structure),
        Region = ParseGeographicRegion(Region),
        Password = Password,
        PlanType = PlanFeatures.Parse(PlanType)
    };

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

    private static GeographicRegion? ParseGeographicRegion(string? region)
    {
        if (string.IsNullOrEmpty(region))
        {
            return null;
        }

        return region.ToLowerInvariant() switch
        {
            "northamerica" => GeographicRegion.NorthAmerica,
            "europe" => GeographicRegion.Europe,
            "asiapacific" => GeographicRegion.AsiaPacific,
            "latinamerica" => GeographicRegion.LatinAmerica,
            "middleeast" => GeographicRegion.MiddleEast,
            "africa" => GeographicRegion.Africa,
            "global" => GeographicRegion.Global,
            _ => throw new ArgumentException($"Unknown region '{region}'. Use: NorthAmerica, Europe, AsiaPacific, LatinAmerica, MiddleEast, Africa, or Global")
        };
    }
}
