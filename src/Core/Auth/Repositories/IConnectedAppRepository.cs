#nullable enable

using Bit.Core.Auth.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.Repositories;

/// <summary>
/// Saves and loads <see cref="ConnectedApp"/>s — the user-key-encrypted agent-access PSK records that are
/// synced to the user's clients.
/// </summary>
public interface IConnectedAppRepository : IRepository<ConnectedApp, Guid>
{
    /// <summary>Loads all of a user's agent connections.</summary>
    Task<ICollection<ConnectedApp>> GetManyByUserIdAsync(Guid userId);

    /// <summary>
    /// Re-wraps the encrypted PSK of a user's agent connections during an account key rotation, within the
    /// rotation transaction.
    /// </summary>
    /// <param name="userId">The user that initiated the key rotation.</param>
    /// <param name="connections">The connections with their re-encrypted <see cref="ConnectedApp.Key"/>.</param>
    DatabaseTransactionAction UpdateForKeyRotation(Guid userId, IEnumerable<ConnectedApp> connections);
}
