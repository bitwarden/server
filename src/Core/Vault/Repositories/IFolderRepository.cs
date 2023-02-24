using Bit.Core.Entities;

namespace Bit.Core.Repositories;

public interface IFolderRepository : IRepository<Folder, Guid>
{
    Task<Folder> GetByIdAsync(Guid id, Guid userId);
    Task<ICollection<Folder>> GetManyByUserIdAsync(Guid userId);
}
