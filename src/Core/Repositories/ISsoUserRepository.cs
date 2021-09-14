using Bit.Core.Models.Table;
using System;
using System.Threading.Tasks;

namespace Bit.Core.Repositories
{
    public interface ISsoUserRepository : IRepository<SsoUser, long>
    {
        Task DeleteAsync(Guid userId, Guid? organizationId);
        Task<SsoUser> GetByUserIdOrganizationIdAsync(Guid organizationId, Guid userId);
    }
}
