using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.OrgUser
{
    public interface IOrganizationUserService
    {
        Task SaveUserAsync(OrganizationUser orgUser, Guid? savingUserId, IEnumerable<SelectionReadOnly> collections);
        Task DeleteUserAsync(Guid organizationId, Guid organizationUserId, Guid? deletingUserId);
        Task<List<(OrganizationUser orgUser, string error)>> DeleteUsersAsync(Guid organizationId,
            IEnumerable<Guid> organizationUsersId, Guid? deletingUserId);
        Task DeleteUserAsync(Guid organizationId, Guid userId);
        Task DeleteAndPushUserRegistrationAsync(Guid organizationId, Guid userId);
        Task UpdateUserGroupsAsync(OrganizationUser organizationUser, IEnumerable<Guid> groupIds, Guid? loggedInUserId);
    }
}
