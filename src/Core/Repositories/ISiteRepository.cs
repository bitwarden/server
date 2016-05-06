using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Domains;

namespace Bit.Core.Repositories
{
    public interface ISiteRepository : IRepository<Site>
    {
        Task<Site> GetByIdAsync(string id, string userId);
        Task<ICollection<Site>> GetManyByUserIdAsync(string userId);
    }
}
