using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListInboxHistoryQuery
{
    /// <summary>
    /// Returns the resolved lease requests (no longer pending) the user can approve, within the history retention
    /// window, for collections the user can Manage. Returns an empty collection when the user manages none.
    /// </summary>
    /// <param name="now">The caller's read clock: it filters/windows the read where applicable, and the derived
    /// statuses stamped on the returned details are computed against the same instant.</param>
    Task<ICollection<AccessRequestDetails>> GetHistoryAsync(Guid userId, DateTime now);
}
