using System;
using Bit.Core.Models.Table;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Bit.Core.Repositories
{
    public interface IFolderRepository : IRepository<Folder, Guid>
    {
        Task<Folder> GetByIdAsync(Guid id, Guid userId);
        Task<ICollection<Folder>> GetManyByUserIdAsync(Guid userId);
    }
}
