using System;
using Bit.Core.Domains;
using System.Threading.Tasks;

namespace Bit.Core.Repositories
{
    public interface IShareRepository : IRepository<Share, Guid>
    {
        Task<Share> GetByIdAsync(Guid id, Guid userId);
    }
}
