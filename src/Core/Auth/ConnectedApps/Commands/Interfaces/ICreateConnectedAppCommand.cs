#nullable enable

using Bit.Core.Auth.Entities;

namespace Bit.Core.Auth.ConnectedApps.Commands.Interfaces;

public interface ICreateConnectedAppCommand
{
    /// <summary>
    /// Stores a new agent-access connection for the user. <paramref name="encryptedKey"/> is the PSK
    /// encrypted with the user's key (ciphertext only — the server never sees the plaintext PSK) and
    /// <paramref name="partnerClientId"/> must resolve to a registered partner. Pushes a create to the
    /// user's other clients.
    /// </summary>
    Task<ConnectedApp> CreateAsync(Guid userId, string partnerClientId, string name, string encryptedKey);
}
