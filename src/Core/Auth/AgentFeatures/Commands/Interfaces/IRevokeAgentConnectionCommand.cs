#nullable enable

namespace Bit.Core.Auth.AgentFeatures.Commands.Interfaces;

public interface IRevokeAgentConnectionCommand
{
    /// <summary>
    /// Revokes (deletes) the user's agent-access connection and pushes a delete so the user's other clients
    /// drop the PSK. The partner's short-lived token expires on its own (no refresh).
    /// </summary>
    Task RevokeAsync(Guid userId, Guid connectionId);
}
