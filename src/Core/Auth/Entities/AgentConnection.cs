#nullable enable

using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Entities;

/// <summary>
/// A user's agent-access connection to a pre-registered "Login with Bitwarden" partner. Stores the
/// agent-access PSK encrypted with the user's key (<see cref="Key"/>) so it can be synced to all of the
/// user's clients, which sit on the other end of the agent-access pipeline. The server never sees the
/// plaintext PSK — <see cref="Key"/> is ciphertext the server cannot decrypt.
/// </summary>
public class AgentConnection : ITableObject<Guid>
{
    public Guid Id { get; set; }

    /// <summary>The user that owns this connection.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The registered partner's OAuth client id (e.g. <c>partner.acme</c>). Identifies which external
    /// integration this connection is for; not unique — a user may have several.
    /// </summary>
    public string PartnerClientId { get; set; } = string.Empty;

    /// <summary>Display name for the connection, shown in the user's connected-apps list.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The agent-access PSK, encrypted with the user's key. Opaque to the server.</summary>
    public string Key { get; set; } = string.Empty;

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    /// <summary>When set, the connection has been revoked and clients should drop the PSK.</summary>
    public DateTime? RevokedDate { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
