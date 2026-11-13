using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListInboxRequestsQuery
{
    /// <summary>Returns the pending requests on the collections the user can Manage.</summary>
    Task<ICollection<AccessRequestDetails>> GetPendingAsync(Guid userId, DateTime now);

    /// <summary>
    /// Returns the approved requests on the collections the user can Manage that can still give access: not started
    /// and inside their window, or behind a live lease.
    /// </summary>
    Task<ICollection<AccessRequestDetails>> GetApprovedAsync(Guid userId, DateTime now);
}
