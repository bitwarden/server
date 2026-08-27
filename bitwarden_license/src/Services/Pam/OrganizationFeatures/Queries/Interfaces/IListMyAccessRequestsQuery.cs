using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListMyAccessRequestsQuery
{
    /// <summary>
    /// Returns the caller's own lease requests across every organization they belong to: everything still live
    /// (awaiting a decision, or approved with an unlapsed window) at any age, plus resolved requests inside the
    /// shared history retention window. Most recent first, capped server-side.
    /// </summary>
    /// <param name="now">The caller's read clock: it filters/windows the read where applicable, and the derived
    /// statuses stamped on the returned details are computed against the same instant.</param>
    Task<ICollection<AccessRequestDetails>> GetMineAsync(Guid userId, DateTime now);
}
