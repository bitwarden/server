using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;

namespace Bit.Core.Dirt.Services;

/// <summary>
/// Handles the type-specific work for a single <see cref="OrganizationDeleteTask"/>. One
/// implementation exists per <see cref="OrganizationDeleteTaskType"/>; the cleanup job resolves the
/// matching handler by <see cref="TaskType"/> and drives it batch-by-batch. A team can add a new
/// cleanup type by adding an enum value and registering a handler — no job changes required.
/// </summary>
public interface IOrganizationDeleteTaskHandler
{
    /// <summary>
    /// The task type this handler is responsible for. Each type must have exactly one handler.
    /// </summary>
    OrganizationDeleteTaskType TaskType { get; }

    /// <summary>
    /// Deletes a single batch of data for the given task and returns the number of items deleted.
    /// Return 0 when there is nothing left to delete, which signals completion. The job calls this
    /// repeatedly within a bounded run budget, persisting progress between calls, so implementations
    /// should delete a bounded amount per call and be safely resumable across runs.
    /// </summary>
    Task<int> DeleteBatchAsync(OrganizationDeleteTask task, CancellationToken cancellationToken);
}
