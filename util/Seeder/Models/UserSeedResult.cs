namespace Bit.Seeder.Models;

/// <summary>
/// Result of seeding a standalone user account with summary statistics for created ciphers and folders.
/// </summary>
public record UserSeedResult(
    Guid UserId,
    string? Email,
    string? ApiKey,
    string? Password,
    bool Premium,
    int CiphersCount,
    int FoldersCount)
{
    internal static UserSeedResult From(PipelineExecutionResult result) =>
        new(result.UserId!.Value,
            result.OwnerEmail,
            result.UserApiKey,
            result.Password,
            result.Premium,
            result.CiphersCount,
            result.FoldersCount);
}
