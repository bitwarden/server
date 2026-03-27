namespace Bit.Seeder.Models;

/// <summary>
/// Result of seeding an organization with summary statistics for created users, groups, collections, and ciphers.
/// </summary>
public record OrganizationSeedResult(
    Guid OrganizationId,
    string? OwnerEmail,
    string? ApiKey,
    string? Password,
    int UsersCount,
    int GroupsCount,
    int CollectionsCount,
    int CiphersCount)
{
    internal static OrganizationSeedResult From(PipelineExecutionResult result) =>
        new(result.OrganizationId!.Value,
            result.OwnerEmail,
            result.OrganizationApiKey,
            result.Password,
            result.UsersCount,
            result.GroupsCount,
            result.CollectionsCount,
            result.CiphersCount);
}
