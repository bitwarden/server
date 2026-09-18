using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListInboxHistoryQuery
{
    /// <summary>
    /// Returns the resolved lease requests within the history retention window for collections the user can
    /// Manage; empty if the user manages none.
    /// </summary>
    /// <param name="now">The caller's read clock, for windowing and for the derived statuses stamped on the result.</param>
    Task<ICollection<AccessRequestDetails>> GetHistoryAsync(Guid userId, DateTime now);
}
