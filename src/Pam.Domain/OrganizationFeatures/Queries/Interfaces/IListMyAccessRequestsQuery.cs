using Bit.Pam.Models;

namespace Bit.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListMyAccessRequestsQuery
{
    /// <summary>
    /// Returns the caller's own lease requests across every organization they belong to, regardless of status. Returns
    /// an empty collection when they have none.
    /// </summary>
    Task<ICollection<AccessRequestDetails>> GetMineAsync(Guid userId);
}
