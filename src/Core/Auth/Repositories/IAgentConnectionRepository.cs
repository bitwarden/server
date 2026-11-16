#nullable enable

using Bit.Core.Auth.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.Repositories;

/// <summary>
/// Saves and loads <see cref="AgentConnection"/>s — the user-key-encrypted agent-access PSK records that are
/// synced to the user's clients.
/// </summary>
public interface IAgentConnectionRepository : IRepository<AgentConnection, Guid>
{
    /// <summary>Loads all of a user's agent connections.</summary>
    Task<ICollection<AgentConnection>> GetManyByUserIdAsync(Guid userId);

    /// <summary>
    /// Re-wraps the encrypted PSK of a user's agent connections during an account key rotation, within the
    /// rotation transaction.
    /// </summary>
    /// <param name="userId">The user that initiated the key rotation.</param>
    /// <param name="connections">The connections with their re-encrypted <see cref="AgentConnection.Key"/>.</param>
    DatabaseTransactionAction UpdateForKeyRotation(Guid userId, IEnumerable<AgentConnection> connections);
}
