namespace Bit.Core.Auth.Models;

/// <summary>
/// Push payload telling a user's clients an <c>AgentConnection</c> changed so they can re-sync the
/// encrypted PSK (on create) or drop it (on revoke/delete).
/// </summary>
public class SyncAgentConnectionPushNotification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime RevisionDate { get; set; }
}
