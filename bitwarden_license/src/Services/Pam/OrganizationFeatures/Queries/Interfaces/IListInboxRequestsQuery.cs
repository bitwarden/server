using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListInboxRequestsQuery
{
    /// <summary>
    /// Returns the pending lease requests the user can approve — those on collections the user can Manage.
    /// </summary>
    /// <param name="now">The caller's read clock.</param>
    Task<ICollection<AccessRequestDetails>> GetPendingAsync(Guid userId, DateTime now);
}
