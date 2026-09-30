using Bit.Pam.Entities;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListActiveLeasesQuery
{
    /// <summary>
    /// Returns every active lease on the collections the caller can Manage — the governance view of all active
    /// access in the caller's scope, not just their own leases. Scope is resolved the same way as the approver
    /// inbox (<see cref="IListInboxRequestsQuery"/>).
    /// </summary>
    /// <param name="now">The caller's read clock, so a lease returned as active cannot render as expired.</param>
    Task<ICollection<AccessLease>> GetActiveAsync(Guid userId, DateTime now);
}
