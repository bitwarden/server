namespace Bit.Seeder.Models;

/// <summary>
/// Internal result of pipeline execution with entity IDs and counts for either an organization or individual user seed.
/// </summary>
internal record ExecutionResult(
    Guid? OrganizationId,
    Guid? UserId,
    string? OwnerEmail,
    string? UserApiKey,
    string? OrganizationApiKey,
    string? Password,
    bool Premium,
    int UsersCount,
    int GroupsCount,
    int CollectionsCount,
    int CiphersCount,
    int FoldersCount);
