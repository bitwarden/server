namespace Bit.Seeder.Models;

internal record SeedPreset
{
    public SeedPresetOrganization? Organization { get; init; }
    public SeedPresetRoster? Roster { get; init; }
    public SeedPresetUsers? Users { get; init; }
    public SeedPresetGroups? Groups { get; init; }
    public SeedPresetCollections? Collections { get; init; }
    public bool? Folders { get; init; }
    public List<string>? FolderNames { get; init; }
    public SeedPresetCiphers? Ciphers { get; init; }
    public SeedPresetPersonalCiphers? PersonalCiphers { get; init; }
    public int? KdfIterations { get; init; }
    public SeedPresetDensity? Density { get; init; }
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
    public int? Seats { get; init; }
    public string? PlanType { get; init; }
}

internal record SeedPresetRoster
{
    public string? Fixture { get; init; }
}

internal record SeedPresetUsers
{
    public int Count { get; init; }
    public bool RealisticStatusMix { get; init; }
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
}

internal record SeedPresetPersonalCiphers
{
    public int CountPerUser { get; init; }
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
