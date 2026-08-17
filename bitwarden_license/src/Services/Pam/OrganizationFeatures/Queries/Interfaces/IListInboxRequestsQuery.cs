using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListInboxRequestsQuery
{
    /// <summary>
    /// Returns the pending lease requests the user can approve — those on collections the user can Manage. Returns an
    /// empty collection when the user manages none.
    /// </summary>
    Task<ICollection<AccessRequestDetails>> GetPendingAsync(Guid userId);
}
