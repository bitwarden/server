using Bit.Seeder.Data.Enums;
using Bit.Seeder.Options;

namespace Bit.Seeder.Data.Distributions;

/// <summary>
/// Named density profiles for CLI usage. Size-independent — user controls entity counts
/// via <c>-u</c>, <c>-g</c>, <c>-c</c>, <c>-l</c> flags separately.
/// </summary>
public static class DensityProfiles
{
    /// <summary>
    /// Balanced mid-market org. Even split between direct and group-mediated access.
    /// Archetype: Sterling Cooper / Wayne Enterprises.
    /// </summary>
    public static DensityProfile Balanced { get; } = new()
    {
        MembershipShape = MembershipDistributionShape.PowerLaw,
        MembershipSkew = 0.6,
        CollectionFanOutMin = 1,
        CollectionFanOutMax = 5,
        FanOutShape = CollectionFanOutShape.PowerLaw,
        EmptyGroupRate = 0.1,
        DirectAccessRatio = 0.5,
        PermissionDistribution = PermissionDistributions.MidMarket,
        UserCollectionMin = 1,
        UserCollectionMax = 10,
        UserCollectionShape = CollectionFanOutShape.PowerLaw,
        UserCollectionSkew = 0.5,
        CipherSkew = CipherCollectionSkew.HeavyRight,
        OrphanCipherRate = 0.08,
        MultiCollectionRate = 0.20,
        MaxCollectionsPerCipher = 3,
        PersonalCipherDistribution = PersonalCipherDistributions.Realistic,
        FolderDistribution = FolderCountDistributions.Realistic,
    };

    /// <summary>
    /// High permission density with steep power-law membership and enterprise read-heavy permissions.
    /// Archetype: Tyrell Corp / Bluth Company.
    /// </summary>
    public static DensityProfile HighPerm { get; } = new()
    {
        MembershipShape = MembershipDistributionShape.PowerLaw,
        MembershipSkew = 0.8,
        CollectionFanOutMin = 2,
        CollectionFanOutMax = 8,
        FanOutShape = CollectionFanOutShape.PowerLaw,
        EmptyGroupRate = 0.2,
        DirectAccessRatio = 0.6,
        PermissionDistribution = PermissionDistributions.Enterprise,
        UserCollectionMin = 1,
        UserCollectionMax = 30,
        UserCollectionShape = CollectionFanOutShape.PowerLaw,
        UserCollectionSkew = 0.7,
        CipherSkew = CipherCollectionSkew.HeavyRight,
        OrphanCipherRate = 0.15,
        MultiCollectionRate = 0.30,
        MaxCollectionsPerCipher = 4,
        PersonalCipherDistribution = PersonalCipherDistributions.Realistic,
        FolderDistribution = FolderCountDistributions.Enterprise,
    };

    /// <summary>
    /// Mega-group with high collection count and write-heavy permissions.
    /// Archetype: Umbrella Corp.
    /// </summary>
    public static DensityProfile HighCollection { get; } = new()
    {
        MembershipShape = MembershipDistributionShape.MegaGroup,
        MembershipSkew = 0.5,
        CollectionFanOutMin = 1,
        CollectionFanOutMax = 3,
        FanOutShape = CollectionFanOutShape.FrontLoaded,
        EmptyGroupRate = 0.0,
        DirectAccessRatio = 0.9,
        PermissionDistribution = PermissionDistributions.MidMarketWriteHeavy,
        UserCollectionMin = 1,
        UserCollectionMax = 15,
        UserCollectionShape = CollectionFanOutShape.PowerLaw,
        UserCollectionSkew = 0.6,
        CipherSkew = CipherCollectionSkew.HeavyRight,
        OrphanCipherRate = 0.20,
        MultiCollectionRate = 0.25,
        MaxCollectionsPerCipher = 3,
        PersonalCipherDistribution = PersonalCipherDistributions.Realistic,
        FolderDistribution = FolderCountDistributions.Realistic,
    };

    /// <summary>
    /// Extreme mega-group with all-direct access and very high orphan rate.
    /// Archetype: Initech (Baker McKenzie production pattern).
    /// </summary>
    public static DensityProfile Broad { get; } = new()
    {
        MembershipShape = MembershipDistributionShape.MegaGroup,
        MembershipSkew = 0.95,
        CollectionFanOutMin = 1,
        CollectionFanOutMax = 2,
        FanOutShape = CollectionFanOutShape.Uniform,
        EmptyGroupRate = 0.0,
        DirectAccessRatio = 1.0,
        PermissionDistribution = PermissionDistributions.EnterpriseManageHeavy,
        UserCollectionMin = 1,
        UserCollectionMax = 20,
        UserCollectionShape = CollectionFanOutShape.PowerLaw,
        UserCollectionSkew = 0.5,
        CipherSkew = CipherCollectionSkew.HeavyRight,
        OrphanCipherRate = 0.85,
        MultiCollectionRate = 0.15,
        MaxCollectionsPerCipher = 3,
        PersonalCipherDistribution = PersonalCipherDistributions.LightUsage,
        FolderDistribution = FolderCountDistributions.Minimal,
    };

    /// <summary>
    /// Low-complexity family/starter org with uniform distributions and no orphans.
    /// Archetype: Central Perk.
    /// </summary>
    public static DensityProfile Minimal { get; } = new()
    {
        MembershipShape = MembershipDistributionShape.Uniform,
        MembershipSkew = 0.0,
        CollectionFanOutMin = 1,
        CollectionFanOutMax = 2,
        FanOutShape = CollectionFanOutShape.Uniform,
        EmptyGroupRate = 0.0,
        DirectAccessRatio = 0.8,
        PermissionDistribution = PermissionDistributions.Family,
        UserCollectionMin = 1,
        UserCollectionMax = 3,
        UserCollectionShape = CollectionFanOutShape.Uniform,
        UserCollectionSkew = 0.0,
        CipherSkew = CipherCollectionSkew.Uniform,
        OrphanCipherRate = 0.0,
        MultiCollectionRate = 0.20,
        MaxCollectionsPerCipher = 2,
        PersonalCipherDistribution = PersonalCipherDistributions.Realistic,
        FolderDistribution = FolderCountDistributions.Realistic,
    };

    /// <summary>
    /// Almost all access via groups, very low direct access. Tests CollectionGroup-heavy code paths.
    /// </summary>
    public static DensityProfile GroupHeavy { get; } = new()
    {
        MembershipShape = MembershipDistributionShape.PowerLaw,
        MembershipSkew = 0.7,
        CollectionFanOutMin = 2,
        CollectionFanOutMax = 6,
        FanOutShape = CollectionFanOutShape.PowerLaw,
        EmptyGroupRate = 0.1,
        DirectAccessRatio = 0.1,
        PermissionDistribution = PermissionDistributions.MidMarketWriteHeavy,
        UserCollectionMin = 1,
        UserCollectionMax = 8,
        UserCollectionShape = CollectionFanOutShape.PowerLaw,
        UserCollectionSkew = 0.5,
        CipherSkew = CipherCollectionSkew.HeavyRight,
        OrphanCipherRate = 0.10,
        MultiCollectionRate = 0.20,
        MaxCollectionsPerCipher = 3,
        PersonalCipherDistribution = PersonalCipherDistributions.Realistic,
        FolderDistribution = FolderCountDistributions.Realistic,
    };

    /// <summary>
    /// Low access density — few groups per collection, few collections per user, high orphan rate.
    /// Models orgs where most users have minimal access and most ciphers are unassigned.
    /// </summary>
    public static DensityProfile Sparse { get; } = new()
    {
        MembershipShape = MembershipDistributionShape.PowerLaw,
        MembershipSkew = 0.5,
        CollectionFanOutMin = 1,
        CollectionFanOutMax = 2,
        FanOutShape = CollectionFanOutShape.Uniform,
        EmptyGroupRate = 0.3,
        DirectAccessRatio = 0.3,
        PermissionDistribution = PermissionDistributions.Enterprise,
        UserCollectionMin = 1,
        UserCollectionMax = 3,
        UserCollectionShape = CollectionFanOutShape.Uniform,
        UserCollectionSkew = 0.0,
        CipherSkew = CipherCollectionSkew.HeavyRight,
        OrphanCipherRate = 0.30,
        MultiCollectionRate = 0.10,
        MaxCollectionsPerCipher = 2,
        PersonalCipherDistribution = PersonalCipherDistributions.LightUsage,
        FolderDistribution = FolderCountDistributions.Minimal,
    };

    /// <summary>
    /// Parses a profile name to a <see cref="DensityProfile"/>. Returns null for null/empty input.
    /// </summary>
    public static DensityProfile? Parse(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return name.ToLowerInvariant() switch
        {
            "balanced" => Balanced,
            "highperm" => HighPerm,
            "highcollection" => HighCollection,
            "broad" => Broad,
            "minimal" => Minimal,
            "groupheavy" => GroupHeavy,
            "sparse" => Sparse,
            _ => throw new ArgumentException(
                $"Unknown density profile '{name}'. Use: balanced, highPerm, highCollection, broad, minimal, groupHeavy, or sparse")
        };
    }
}
