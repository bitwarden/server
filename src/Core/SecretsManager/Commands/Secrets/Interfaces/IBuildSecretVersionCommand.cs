using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Secrets.Interfaces;

public interface IBuildSecretVersionCommand
{
    /// <summary>
    /// Builds the version snapshot for a secret without persisting it. The caller hands the result
    /// to <see cref="Repositories.ISecretRepository"/>, which writes the secret and its version in
    /// a single transaction.
    /// </summary>
    Task<SecretVersion> BuildAsync(Secret secret, Guid accessClientId);
}
