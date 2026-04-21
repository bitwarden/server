#nullable enable

using Bit.Core.Dirt.Entities;

namespace Bit.Core.Dirt.Repositories;

public interface IOrganizationEventCleanupRepository
{
    Task<OrganizationEventCleanup?> GetNextPendingAsync();
    Task MarkStartedAsync(Guid id);
    Task IncrementProgressAsync(Guid id, long delta);
    Task MarkCompletedAsync(Guid id);
    Task RecordErrorAsync(Guid id, string message);
}
