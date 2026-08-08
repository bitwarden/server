using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Repositories;

public interface IFolderRepository : IRepository<Folder, Guid>
{
    Task<Folder> GetByIdAsync(Guid id, Guid userId);
    Task<ICollection<Folder>> GetManyByUserIdAsync(Guid userId);

    /// <summary>
    /// Updates encrypted data for folders during a key rotation
    /// </summary>
    /// <param name="userId">The user that initiated the key rotation</param>
    /// <param name="folders">A list of folders with updated data</param>
    DatabaseTransactionAction UpdateForKeyRotation(Guid userId,
        IEnumerable<Folder> folders);
}
