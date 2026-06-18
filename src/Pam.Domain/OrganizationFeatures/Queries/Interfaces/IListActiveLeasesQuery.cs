using Bit.Pam.Entities;

namespace Bit.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListActiveLeasesQuery
{
    /// <summary>
    /// Returns every currently-active lease on the collections the caller can Manage — the governance view of all
    /// active access in the caller's scope, not just their own leases. Scope is resolved the same way as the approver
    /// inbox (<see cref="IListInboxRequestsQuery"/>): the caller's manageable collections across every organization.
    /// Returns an empty collection when the caller manages none.
    /// </summary>
    Task<ICollection<AccessLease>> GetActiveAsync(Guid userId);
}
