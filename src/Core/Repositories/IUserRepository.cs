using System;
using System.Threading.Tasks;
using Bit.Core.Domains;

namespace Bit.Core.Repositories
{
    public interface IUserRepository : IRepository<User, Guid>
    {
        Task<User> GetByEmailAsync(string email);
        Task<string> GetPublicKeyAsync(Guid id);
        Task<DateTime> GetAccountRevisionDateAsync(Guid id);
    }
}
