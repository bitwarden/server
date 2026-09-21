using Bit.Core.Dirt.Entities;

namespace Bit.Core.Dirt.Repositories;

public interface IOrganizationDeleteTaskRepository
{
    Task CreateAsync(OrganizationDeleteTask task);
    Task<OrganizationDeleteTask?> ClaimNextPendingAsync();
    Task UpdateProgressAsync(Guid id, long delta);
    /// <summary>
    /// Records a failure against the task and returns its new failure count, so the caller can tell
    /// when a task has reached <see cref="OrganizationDeleteTask.MaxFailureCount"/> and will no
    /// longer be claimed.
    /// </summary>
    Task<int> UpdateErrorAsync(Guid id, string message);
    Task UpdateCompletedAsync(Guid id);
}
