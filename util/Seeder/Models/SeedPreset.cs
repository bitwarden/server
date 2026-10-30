using System.Text.Json;

namespace Bit.Seeder.Models;

internal record SeedPreset
{
    public SeedPresetOrganization? Organization { get; init; }
    public SeedPresetRoster? Roster { get; init; }
    public SeedPresetUsers? Users { get; init; }
    public SeedPresetGroups? Groups { get; init; }
    public SeedPresetCollections? Collections { get; init; }
    public SeedPresetSso? Sso { get; init; }
    public SeedPresetPolicies? Policies { get; init; }
    public SeedPresetDefaultCollections? DefaultCollections { get; init; }
    public bool? Folders { get; init; }
    public List<string>? FolderNames { get; init; }
    public SeedPresetCiphers? Ciphers { get; init; }
    public SeedPresetPersonalCiphers? PersonalCiphers { get; init; }
    public int? KdfIterations { get; init; }
    public SeedPresetDensity? Density { get; init; }
    public SeedPresetAccessShape? AccessShape { get; init; }
    public List<SeedCollectionAssignment>? CollectionAssignments { get; init; }
    public List<SeedFolderAssignment>? FolderAssignments { get; init; }
    public List<SeedFavoriteAssignment>? FavoriteAssignments { get; init; }
    public SeedPresetIndividualUser? User { get; init; }
    internal bool IsIndividual => User is not null;
}

internal record SeedPresetOrganization
{
    public string? Fixture { get; init; }
    public string? Name { get; init; }
    public string? Domain { get; init; }
    public List<string>? ClaimedDomains { get; init; }
    public int? Seats { get; init; }
    public string? PlanType { get; init; }
    public bool? UseAutomaticUserConfirmation { get; init; }
    public bool? AllowAdminAccessToAllCollectionItems { get; init; }
    public bool? LimitItemDeletion { get; init; }
    public bool? LimitCollectionCreation { get; init; }
    public bool? LimitCollectionDeletion { get; init; }
}

internal record SeedPresetSso
{
    public string? Identifier { get; init; }
    public string? EncryptionType { get; init; }
    public string? Provider { get; init; }
}

/// <summary>
/// Policy rows to create, keyed by <c>PolicyType</c> name. <see cref="EnableAll"/> and <see cref="Except"/> are
/// not supported; they exist only so <c>PresetValidator</c> can reject them instead of silently dropping them.
/// </summary>
internal record SeedPresetPolicies
{
    public bool? EnableAll { get; init; }
    public List<string>? Except { get; init; }
    public List<string>? Enable { get; init; }
    public List<string>? Disable { get; init; }
    public Dictionary<string, JsonElement>? Data { get; init; }
}

/// <summary>
/// Default user collections ("My Items") as the Organization Data Ownership policy creates them, with the personal
/// items members have already migrated into them.
/// </summary>
internal record SeedPresetDefaultCollections
{
    public string? Name { get; init; }
    public double? NonEmptyRate { get; init; }
    public List<SeedPresetSizeBucket>? Sizes { get; init; }
    public List<int>? Largest { get; init; }
    public double? DeletedRate { get; init; }
    public int? PolicyEnabledDaysAgo { get; init; }
}

internal record SeedPresetSizeBucket
{
    public int Min { get; init; }
    public int Max { get; init; }
    public double Weight { get; init; }
}

internal record SeedPresetRoster
{
    public string? Fixture { get; init; }
}

internal record SeedPresetUsers
{
    public int Count { get; init; }
    public bool RealisticStatusMix { get; init; }
    public SeedPresetStatusMix? StatusMix { get; init; }
    public int AdminCount { get; init; }
}

/// <summary>
/// Relative member status weights; the loader normalizes them, so they need not sum to 1.
/// </summary>
internal record SeedPresetStatusMix
{
    public double Confirmed { get; init; }
    public double Invited { get; init; }
    public double Accepted { get; init; }
    public double Revoked { get; init; }
}

internal record SeedPresetGroups
{
    public int Count { get; init; }
}

internal record SeedPresetCollections
{
    public int Count { get; init; }
}

internal record SeedPresetCiphers
{
    public string? Fixture { get; init; }
    public int Count { get; init; }
    public bool AssignFolders { get; init; }
    public int RepromptEveryNthCipher { get; init; }
}

internal record SeedPresetPersonalCiphers
{
    public int CountPerUser { get; init; }
    public int RepromptEveryNthCipher { get; init; }
}

internal record SeedCollectionAssignment
{
    public required string Cipher { get; init; }
    public required string Collection { get; init; }
}

internal record SeedFolderAssignment
{
    public required string Cipher { get; init; }
    public required string Folder { get; init; }
    public required string User { get; init; }
}

internal record SeedFavoriteAssignment
{
    public required string Cipher { get; init; }
    public required string User { get; init; }
}

internal record SeedPresetIndividualUser
{
    public required string Email { get; init; }
    public required bool Premium { get; init; }
    public required short MaxStorageGb { get; init; }
}
