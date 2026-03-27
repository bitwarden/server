namespace Bit.Seeder.Models;

/// <summary>
/// Result of individual user seeding with summary statistics.
/// </summary>
public record IndividualSeedResult(
    Guid UserId,
    string? Email,
    string? ApiKey,
    string? Password,
    bool Premium,
    int CiphersCount,
    int FoldersCount)
{
    internal static IndividualSeedResult From(ExecutionResult result) =>
        new(result.UserId!.Value,
            result.OwnerEmail,
            result.UserApiKey,
            result.Password,
            result.Premium,
            result.CiphersCount,
            result.FoldersCount);
}
