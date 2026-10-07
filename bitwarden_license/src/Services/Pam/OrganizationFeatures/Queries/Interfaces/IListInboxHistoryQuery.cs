using Bit.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListInboxHistoryQuery
{
    /// <summary>
    /// Returns the resolved requests within the history window on the collections the user can Manage.
    /// </summary>
    Task<ICollection<AccessRequestDetails>> GetHistoryAsync(Guid userId, DateTime now);
}
