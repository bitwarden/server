using System;
using System.Threading.Tasks;
using Bit.Core.Domains;

namespace Bit.Core.Repositories
{
    public interface ISubvaultUserRepository : IRepository<SubvaultUser, Guid>
    {
        
    }
}
