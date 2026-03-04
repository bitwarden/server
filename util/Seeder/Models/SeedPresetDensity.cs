namespace Bit.Seeder.Models;

/// <summary>
/// Top-level density block in a preset JSON. Controls relationship patterns between entities.
/// </summary>
internal record SeedPresetDensity
{
    public SeedPresetMembership? Membership { get; init; }

    public SeedPresetCollectionFanOut? CollectionFanOut { get; init; }

    public double? DirectAccessRatio { get; init; }

    public SeedPresetPermissions? Permissions { get; init; }

    public SeedPresetCipherAssignment? CipherAssignment { get; init; }

    public SeedPresetUserCollections? UserCollections { get; init; }

    public SeedPresetCipherTypes? CipherTypes { get; init; }

    public SeedPresetDensityPersonalCiphers? PersonalCiphers { get; init; }
}

/// <summary>
/// Personal cipher count distribution per user: a named preset shape.
/// </summary>
internal record SeedPresetDensityPersonalCiphers
{
    public string? Shape { get; init; }
}

/// <summary>
/// Cipher type distribution: a named preset or custom weights per type.
/// </summary>
internal record SeedPresetCipherTypes
{
    public string? Preset { get; init; }

    public double? Login { get; init; }

    public double? SecureNote { get; init; }

    public double? Card { get; init; }

    public double? Identity { get; init; }

    public double? SshKey { get; init; }
}

/// <summary>
/// How many direct collections each user receives: range, distribution shape, and skew.
/// </summary>
internal record SeedPresetUserCollections
{
    public int? Min { get; init; }

    public int? Max { get; init; }

    public string? Shape { get; init; }

    public double? Skew { get; init; }
}

/// <summary>
/// How users are distributed across groups (uniform, powerLaw, megaGroup) and skew intensity.
/// </summary>
internal record SeedPresetMembership
{
    public string? Shape { get; init; }

    public double? Skew { get; init; }
}

/// <summary>
/// How collections are assigned to groups: range, distribution shape, and empty group rate.
/// </summary>
internal record SeedPresetCollectionFanOut
{
    public int? Min { get; init; }

    public int? Max { get; init; }

    public string? Shape { get; init; }

    public double? EmptyGroupRate { get; init; }
}

/// <summary>
/// Permission type weights for collection access assignments. Must sum to 1.0.
/// </summary>
internal record SeedPresetPermissions
{
    public double? Manage { get; init; }

    public double? ReadOnly { get; init; }

    public double? HidePasswords { get; init; }

    public double? ReadWrite { get; init; }
}

/// <summary>
/// How ciphers are distributed across collections: skew shape and orphan rate.
/// </summary>
internal record SeedPresetCipherAssignment
{
    public string? Skew { get; init; }

    public double? OrphanRate { get; init; }

    public double? MultiCollectionRate { get; init; }

    public int? MaxCollectionsPerCipher { get; init; }
}
