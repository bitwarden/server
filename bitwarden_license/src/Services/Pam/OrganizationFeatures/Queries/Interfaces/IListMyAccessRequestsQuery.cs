using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListMyAccessRequestsQuery
{
    /// <summary>
    /// Returns the caller's own lease requests across every organization: everything still live, at any age, plus
    /// resolved requests inside the shared history retention window. Most recent first, capped server-side.
    /// </summary>
    /// <param name="now">The caller's read clock, for windowing and for the derived statuses stamped on the result.</param>
    Task<ICollection<AccessRequestDetails>> GetMineAsync(Guid userId, DateTime now);
}
