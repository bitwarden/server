using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Repositories
{
    public interface ISsoUserRepository : IRepository<SsoUser, long>
    {
        Task DeleteAsync(Guid userId, Guid? organizationId);
        Task<SsoUser> GetByUserIdOrganizationIdAsync(Guid organizationId, Guid userId);
    }
}
