using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Repositories;

public interface IFolderRepository : IRepository<Folder, Guid>
{
    Task<Folder?> GetByIdAsync(Guid id, Guid userId);
    Task<ICollection<Folder>> GetManyByUserIdAsync(Guid userId);

    /// <summary>
    /// Deletes the given folders and re-assigns any of the user's ciphers that were filed under them to no folder.
    /// Ids that do not belong to the user are ignored.
    /// </summary>
    /// <param name="folderIds">The folders to delete</param>
    /// <param name="userId">The owner of the folders</param>
    Task DeleteManyAsync(IEnumerable<Guid> folderIds, Guid userId);

    /// <summary>
    /// Updates encrypted data for folders during a key rotation
    /// </summary>
    /// <param name="userId">The user that initiated the key rotation</param>
    /// <param name="folders">A list of folders with updated data</param>
    DatabaseTransactionAction UpdateForKeyRotation(Guid userId,
        IEnumerable<Folder> folders);
}
