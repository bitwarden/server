using Bit.Pam.Entities;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListLeaseHistoryQuery
{
    /// <summary>Returns the ended leases on the collections the caller can Manage, within the history window.</summary>
    /// <param name="now">Decides which leases count as ended, since nothing writes Expired.</param>
    Task<ICollection<AccessLease>> GetHistoryAsync(Guid userId, DateTime now);
}
