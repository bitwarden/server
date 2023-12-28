using Bit.Core.Auth.UserFeatures.UserKey;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Repositories;

public interface IFolderRepository : IRepository<Folder, Guid>
{
    Task<Folder> GetByIdAsync(Guid id, Guid userId);
    Task<ICollection<Folder>> GetManyByUserIdAsync(Guid userId);

    [Obsolete("Not used while FlexibleCollections feature flag is in use - use the overload to pass in the flag value instead.")]
    new Task DeleteAsync(Folder folder);
    Task DeleteAsync(Folder folder, bool useFlexibleCollections);

    /// <summary>
    /// Updates encrypted data for folders during a key rotation
    /// </summary>
    /// <param name="userId">The user that initiated the key rotation</param>
    /// <param name="folders">A list of folders with updated data</param>
    UpdateEncryptedDataForKeyRotation UpdateForKeyRotation(Guid userId,
        IEnumerable<Folder> folders);
}
