using Bit.Pam.Models;

namespace Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListInboxHistoryQuery
{
    /// <summary>
    /// Returns the resolved lease requests (no longer pending) the user can approve, within the history retention
    /// window, for collections the user can Manage. Returns an empty collection when the user manages none.
    /// </summary>
    Task<ICollection<AccessRequestDetails>> GetHistoryAsync(Guid userId);
}
