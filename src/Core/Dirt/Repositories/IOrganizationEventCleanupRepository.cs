using Bit.Core.Dirt.Entities;

namespace Bit.Core.Dirt.Repositories;

public interface IOrganizationEventCleanupRepository
{
    Task CreateAsync(OrganizationEventCleanup cleanup);
    Task<OrganizationEventCleanup?> ClaimNextPendingAsync();
    Task UpdateProgressAsync(Guid id, long delta);
    Task UpdateErrorAsync(Guid id, string message);
    Task UpdateCompletedAsync(Guid id);
}
