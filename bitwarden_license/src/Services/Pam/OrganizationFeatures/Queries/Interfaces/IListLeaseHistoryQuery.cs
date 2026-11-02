using Bit.Pam.Entities;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListLeaseHistoryQuery
{
    /// <summary>
    /// Returns the ended leases (expired or revoked) on the collections the caller can Manage, within the shared
    /// history window. Scope is resolved the same way as the approver inbox (<see cref="IListInboxRequestsQuery"/>).
    /// </summary>
    /// <param name="now">The caller's read clock; anchors the retention window and decides which leases count as
    /// ended, since nothing writes Expired.</param>
    Task<ICollection<AccessLease>> GetHistoryAsync(Guid userId, DateTime now);
}
