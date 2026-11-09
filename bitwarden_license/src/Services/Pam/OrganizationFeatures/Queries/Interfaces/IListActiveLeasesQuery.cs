using Bit.Pam.Entities;

namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListActiveLeasesQuery
{
    /// <summary>Returns every member's active leases on the collections the caller can Manage.</summary>
    /// <param name="now">The caller's read clock, so a lease returned as active cannot render as expired.</param>
    Task<ICollection<AccessLease>> GetActiveAsync(Guid userId, DateTime now);
}
