using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListInboxRequestsQuery
{
    /// <summary>
    /// Returns the pending lease requests the user can approve — those on collections the user can Manage. Returns an
    /// empty collection when the user manages none.
    /// </summary>
    /// <param name="now">The caller's read clock: it filters/windows the read where applicable, and the derived
    /// statuses stamped on the returned details are computed against the same instant.</param>
    Task<ICollection<AccessRequestDetails>> GetPendingAsync(Guid userId, DateTime now);
}
