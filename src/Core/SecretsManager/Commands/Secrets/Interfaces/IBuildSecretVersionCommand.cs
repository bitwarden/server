using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Secrets.Interfaces;

/// <summary>
/// Builds the <see cref="SecretVersion"/> snapshot recorded when a secret's value is created or
/// changed, resolving which editor to attribute it to.
/// </summary>
public interface IBuildSecretVersionCommand
{
    /// <summary>
    /// Builds an unsaved version snapshot of <paramref name="secret"/> from its current value and
    /// revision date, attributed to the acting client. Attribution is split by client type: a
    /// service account is recorded directly, while a user is resolved to their organization user
    /// in the secret's organization.
    /// </summary>
    /// <param name="secret">The secret to snapshot. Its value and revision date are copied as-is.</param>
    /// <param name="accessClientId">
    /// The acting client's ID - a service account ID when the request is authenticated as a service
    /// account, otherwise the user ID.
    /// </param>
    /// <returns>
    /// An unpersisted snapshot. Callers pass it to
    /// <see cref="Bit.Core.SecretsManager.Repositories.ISecretRepository"/> so the secret and its
    /// version are written in one transaction.
    /// </returns>
    Task<SecretVersion> BuildAsync(Secret secret, Guid accessClientId);
}
