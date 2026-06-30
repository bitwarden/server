using Bit.Pam.Models;

namespace Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListAccessAuditTrailQuery
{
    /// <summary>
    /// Returns the synthesized access-audit trail for the collections the caller can Manage, within the shared history
    /// window — the governance audit view in the caller's scope, newest first. Scope is resolved the same way as the
    /// approver inbox and lease history (<see cref="IListLeaseHistoryQuery"/>): the caller's manageable collections
    /// across every organization. Events are projected from existing PAM entity state; nothing is persisted. Returns an
    /// empty collection when the caller manages no collections.
    /// </summary>
    Task<ICollection<AccessAuditEvent>> GetTrailAsync(Guid userId);
}
