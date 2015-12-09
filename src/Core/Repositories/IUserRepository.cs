using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Domains;

namespace Bit.Core.Repositories
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User> GetByEmailAsync(string email);
        Task ReplaceAndDirtyCiphersAsync(User user);
    }
}
