using Bit.Core.Pam.Models;

namespace Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IGetInboxHistoryQuery
{
    /// <summary>
    /// Returns the resolved lease requests (no longer pending) the user can approve, within the history retention
    /// window, for collections the user can Manage. Returns an empty collection when the user manages none.
    /// </summary>
    Task<ICollection<InboxLeaseRequestDetails>> GetHistoryAsync(Guid userId);
}
