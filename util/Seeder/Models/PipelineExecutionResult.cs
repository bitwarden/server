namespace Bit.Seeder.Models;

/// <summary>
/// Internal result produced by the recipe pipeline, carrying entity IDs and counts before being mapped to a public result type.
/// </summary>
internal record PipelineExecutionResult(
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
