using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;

namespace Bit.Core.Vault.Queries;

public interface IGetTasksForOrganizationQuery
{
    /// <summary>
    /// Retrieves all security tasks for an organization.
    /// </summary>
    /// <param name="organizationId">The Id of the organization</param>
    /// <param name="status">Optional filter for task status. If not provided, returns tasks of all statuses</param>
    /// <returns>A collection of security tasks</returns>
    Task<ICollection<SecurityTask>> GetTasksAsync(Guid organizationId, SecurityTaskStatus? status = null);
}
