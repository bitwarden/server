using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Domains;

namespace Bit.Core.Repositories
{
    public interface IFolderRepository : IRepository<Folder>
    {
        Task<Folder> GetByIdAsync(string id, string userId);
        Task<ICollection<Folder>> GetManyByUserIdAsync(string userId);
        Task<ICollection<Folder>> GetManyByUserIdAsync(string userId, bool dirty);
    }
}
