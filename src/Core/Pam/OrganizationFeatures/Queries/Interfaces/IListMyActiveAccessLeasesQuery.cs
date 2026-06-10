using Bit.Core.Pam.Entities;

namespace Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IListMyActiveAccessLeasesQuery
{
    /// <summary>
    /// Returns the caller's currently-active leases across every organization they belong to. Returns an empty
    /// collection when none are active.
    /// </summary>
    Task<ICollection<AccessLease>> GetMineActiveAsync(Guid userId);
}
