using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories
{
    public interface ISubvaultUserRepository : IRepository<SubvaultUser, Guid>
    {
        Task<ICollection<SubvaultUser>> GetManyByOrganizationUserIdAsync(Guid orgUserId);
        Task<ICollection<SubvaultUserDetails>> GetManyDetailsByUserIdAsync(Guid userId);
        Task<ICollection<SubvaultUserPermissions>> GetPermissionsByUserIdAsync(Guid userId, IEnumerable<Guid> subvaultIds,
            Guid organizationId);
    }
}
