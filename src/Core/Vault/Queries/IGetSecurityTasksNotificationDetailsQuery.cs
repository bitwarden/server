using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Vault.Queries;

public interface IGetSecurityTasksNotificationDetailsQuery
{
    /// <summary>
    /// Retrieves all users within the given organization that are applicable to the given security tasks.
    ///
    /// <param name="organizationId"></param>
    /// <param name="tasks"></param>
    /// <returns>A dictionary of UserIds and the corresponding amount of security tasks applicable to them.</returns>
    /// </summary>
    public Task<ICollection<UserSecurityTaskCipher>> GetNotificationDetailsByManyIds(Guid organizationId, IEnumerable<SecurityTask> tasks);
}
