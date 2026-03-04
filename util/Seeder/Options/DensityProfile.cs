using Bit.Core.Vault.Enums;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Options;

/// <summary>
/// Controls relationship density between users, groups, collections, and ciphers within a seeded organization.
/// When null on <see cref="OrganizationVaultOptions"/>, steps use default round-robin behavior.
/// </summary>
public class DensityProfile
{
    /// <summary>
    /// User-to-group membership distribution shape. Defaults to Uniform (round-robin).
    /// </summary>
    public MembershipDistributionShape MembershipShape { get; init; } = MembershipDistributionShape.Uniform;

    /// <summary>
    /// Skew intensity for PowerLaw and MegaGroup shapes (0.0-1.0). Ignored for Uniform.
    /// </summary>
    public double MembershipSkew { get; init; }

    /// <summary>
    /// Minimum collections assigned per non-empty group.
    /// </summary>
    public int CollectionFanOutMin { get; init; } = 1;

    /// <summary>
    /// Maximum collections assigned per non-empty group.
    /// </summary>
    public int CollectionFanOutMax { get; init; } = 3;

    /// <summary>
    /// Distribution shape for group-to-collection fan-out.
    /// </summary>
    public CollectionFanOutShape FanOutShape { get; init; } = CollectionFanOutShape.Uniform;

    /// <summary>
    /// Fraction of groups with zero members (0.0-1.0).
    /// </summary>
    public double EmptyGroupRate { get; init; }

    /// <summary>
    /// Fraction of access paths that are direct CollectionUser assignments (0.0-1.0).
    /// 1.0 = all direct (current default), 0.0 = all group-mediated.
    /// </summary>
    public double DirectAccessRatio { get; init; } = 1.0;

    /// <summary>
    /// Permission type weighting for collection access assignments.
    /// </summary>
    public Distribution<PermissionWeight> PermissionDistribution { get; init; } = PermissionDistributions.Enterprise;

    /// <summary>
    /// Minimum direct collections per user.
    /// </summary>
    public int UserCollectionMin { get; init; } = 1;

    /// <summary>
    /// Maximum direct collections per user.
    /// </summary>
    public int UserCollectionMax { get; init; } = 3;

    /// <summary>
    /// Distribution shape for user-to-collection direct assignments.
    /// </summary>
    public CollectionFanOutShape UserCollectionShape { get; init; } = CollectionFanOutShape.Uniform;

    /// <summary>
    /// Skew intensity for PowerLaw user-collection shape (0.0-1.0). Ignored for Uniform/FrontLoaded.
    /// </summary>
    public double UserCollectionSkew { get; init; }

    /// <summary>
    /// Cipher type distribution override. When null, falls through to Realistic.
    /// </summary>
    public Distribution<CipherType>? CipherTypeDistribution { get; init; }

    /// <summary>
    /// Cipher-to-collection assignment skew shape.
    /// </summary>
    public CipherCollectionSkew CipherSkew { get; init; } = CipherCollectionSkew.Uniform;

    /// <summary>
    /// Fraction of org ciphers with no collection assignment (0.0-1.0).
    /// </summary>
    public double OrphanCipherRate { get; init; }

    /// <summary>
    /// Fraction of non-orphan ciphers assigned to more than one collection (0.0-1.0).
    /// </summary>
    public double MultiCollectionRate { get; init; }

    /// <summary>
    /// Maximum number of collections a multi-collection cipher can belong to.
    /// </summary>
    public int MaxCollectionsPerCipher { get; init; } = 2;

    /// <summary>
    /// Personal cipher count distribution override. When null, uses flat countPerUser.
    /// </summary>
    public Distribution<(int Min, int Max)>? PersonalCipherDistribution { get; init; }
}
