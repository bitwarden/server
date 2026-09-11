using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Api.IntegrationTest.SecretsManager.Helpers;

/// <summary>
/// Seeding helpers that make a test state which version history its secret starts with, now that
/// <see cref="ISecretRepository.CreateAsync"/> requires an initial version.
/// </summary>
public static class SecretSeedExtensions
{
    /// <summary>
    /// Creates a secret with the initial version snapshot that
    /// <c>POST /organizations/{organizationId}/secrets</c> writes, so the seeded secret matches one
    /// created through the API. Use this unless the test is specifically about missing history.
    /// </summary>
    public static Task<Secret> CreateWithInitialVersionAsync(this ISecretRepository secretRepository, Secret secret) =>
        secretRepository.CreateAsync(secret, null, new SecretVersion
        {
            Value = secret.Value ?? string.Empty,
            VersionDate = secret.RevisionDate
        });

    /// <summary>
    /// Creates a secret with no version history, the way secrets stored before versioning existed
    /// sit in the database. Goes through <see cref="ISecretRepository.ImportAsync"/> because import
    /// is the only remaining production path that writes a secret without a version.
    /// </summary>
    public static async Task<Secret> CreateWithoutVersionHistoryAsync(this ISecretRepository secretRepository,
        Secret secret)
    {
        // ImportAsync expects caller-assigned ids, the way ImportCommand.AssignNewIds supplies them.
        secret.SetNewId();
        await secretRepository.ImportAsync([secret]);
        return secret;
    }
}
