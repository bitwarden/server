using Bit.Core.Dirt.Entities;

namespace Bit.Core.Dirt.Repositories;

public interface IOrganizationDeleteTaskRepository
{
    Task CreateAsync(OrganizationDeleteTask task);
    Task<OrganizationDeleteTask?> ClaimNextPendingAsync();
    Task UpdateProgressAsync(Guid id, long delta);
    Task UpdateErrorAsync(Guid id, string message);
    Task UpdateCompletedAsync(Guid id);
}
