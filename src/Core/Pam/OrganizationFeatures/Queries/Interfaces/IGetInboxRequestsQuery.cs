using Bit.Core.Pam.Models;

namespace Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IGetInboxRequestsQuery
{
    /// <summary>
    /// Returns the pending lease requests the user can approve — those on collections the user can Manage. Returns an
    /// empty collection when the user manages none.
    /// </summary>
    Task<ICollection<InboxLeaseRequestDetails>> GetPendingAsync(Guid userId);
}
