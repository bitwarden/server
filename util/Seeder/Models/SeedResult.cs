namespace Bit.Seeder.Models;

/// <summary>
/// Result of organization seeding with summary statistics.
/// </summary>
public record SeedResult(
    Guid OrganizationId,
    string? OwnerEmail,
    string? ApiKey,
    string? Password,
    int UsersCount,
    int GroupsCount,
    int CollectionsCount,
    int CiphersCount)
{
    internal static SeedResult From(ExecutionResult result) =>
        new(result.OrganizationId!.Value,
            result.OwnerEmail,
            result.OrganizationApiKey,
            result.Password,
            result.UsersCount,
            result.GroupsCount,
            result.CollectionsCount,
            result.CiphersCount);
}
