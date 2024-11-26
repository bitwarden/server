using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;

namespace Bit.Core.Vault.Queries;

public interface IGetTaskDetailsForUserQuery
{
    /// <summary>
    /// Retrieves security tasks for a user based on their organization and cipher access permissions.
    /// </summary>
    /// <param name="userId">The Id of the user retrieving tasks</param>
    /// <param name="status">Optional filter for task status. If not provided, returns tasks of all statuses</param>
    /// <returns>A a collection of security tasks</returns>
    Task<IEnumerable<SecurityTask>> GetTaskDetailsForUserAsync(Guid userId, IEnumerable<SecurityTaskStatus> status = null);
}
