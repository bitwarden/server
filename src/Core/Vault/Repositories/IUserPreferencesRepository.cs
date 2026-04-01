using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Repositories;

public interface IUserPreferencesRepository : IRepository<UserPreferences, Guid>
{
    /// <summary>
    ///  Retrieves user preferences by user id. Returns null if no preferences exist for the user.
    /// </summary>
    /// <param name="userId">The id of the user</param>
    /// <returns></returns>
    Task<UserPreferences?> GetByUserIdAsync(Guid userId);

    /// <summary>
    ///  Deletes user preferences by user id.
    /// </summary>
    /// <param name="userId">The id of the user</param>
    /// <returns></returns>
    Task DeleteByUserIdAsync(Guid userId);
}
