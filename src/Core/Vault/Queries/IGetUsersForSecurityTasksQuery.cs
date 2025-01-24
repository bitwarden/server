using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Vault.Queries;

public interface IGetUsersForSecurityTasksQuery
{
    /// <summary>
    /// Retrieves all users within an organization that have actionable security tasks.
    ///
    /// <param name="organizationId"></param>
    /// <param name="tasks"></param>
    /// <returns>A dictionary of UserIds and the corresponding amount of security tasks applicable to them.</returns>
    public Task<ICollection<UserSecurityTasksCount>> GetAllUsersBySecurityTasks(Guid organizationId, IEnumerable<SecurityTask> tasks);
}
