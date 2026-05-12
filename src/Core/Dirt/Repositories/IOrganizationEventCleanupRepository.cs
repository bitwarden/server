using Bit.Core.Dirt.Entities;

namespace Bit.Core.Dirt.Repositories;

public interface IOrganizationEventCleanupRepository
{
    Task CreateAsync(OrganizationEventCleanup cleanup);
    Task<OrganizationEventCleanup?> ReadNextPendingAsync();
    Task MarkStartedAsync(Guid id);
    Task IncrementProgressAsync(Guid id, long delta);
    Task RecordErrorAsync(Guid id, string message);
    Task MarkCompletedAsync(Guid id);
}
