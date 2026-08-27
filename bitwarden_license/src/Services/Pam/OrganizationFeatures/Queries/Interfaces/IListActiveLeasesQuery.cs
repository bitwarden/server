using Bit.Pam.Entities;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListActiveLeasesQuery
{
    /// <summary>
    /// Returns every currently-active lease on the collections the caller can Manage — the governance view of all
    /// active access in the caller's scope, not just their own leases. Scope is resolved the same way as the approver
    /// inbox (<see cref="IListInboxRequestsQuery"/>): the caller's manageable collections across every organization.
    /// Returns an empty collection when the caller manages none.
    /// </summary>
    /// <param name="now">The caller's read clock. It filters the repository read, and it is the same instant the
    /// caller must derive response statuses against — one clock, so a lease returned as active cannot render as
    /// expired.</param>
    Task<ICollection<AccessLease>> GetActiveAsync(Guid userId, DateTime now);
}
