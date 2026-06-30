using Bit.Pam.Entities;

namespace Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListLeaseHistoryQuery
{
    /// <summary>
    /// Returns the ended leases (expired or revoked) on the collections the caller can Manage, within the shared
    /// history window — the governance history view of recently-ended access in the caller's scope. Scope is resolved
    /// the same way as the approver inbox (<see cref="IListInboxRequestsQuery"/>): the caller's manageable collections
    /// across every organization. Returns an empty collection when the caller manages none.
    /// </summary>
    Task<ICollection<AccessLease>> GetHistoryAsync(Guid userId);
}
