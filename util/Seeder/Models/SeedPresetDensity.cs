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
}
