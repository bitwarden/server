using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListInboxRequestsQuery
{
    /// <summary>Returns the pending requests on the collections the user can Manage.</summary>
    Task<ICollection<AccessRequestDetails>> GetPendingAsync(Guid userId, DateTime now);
}
