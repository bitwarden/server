using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Repositories;

public interface IFolderRepository : IRepository<Folder, Guid>
{
    Task<Folder> GetByIdAsync(Guid id, Guid userId);
    Task<ICollection<Folder>> GetManyByUserIdAsync(Guid userId);
}
