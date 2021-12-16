using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.OrgUser
{
    public interface IOrganizationUserAccessPolicies
    {
        Task<AccessPolicyResult> CanSaveAsync(OrganizationUser orgUser, Guid? savingUserId);
        Task<AccessPolicyResult> CanDeleteUserAsync(Guid organizationId, OrganizationUser orgUser, Guid? deletingUserId);
        Task<AccessPolicyResult> CanDeleteManyUsersAsync(Guid organizationId, IEnumerable<OrganizationUser> orgUsers,
            Guid? deletingUserId);
        Task<AccessPolicyResult> CanSelfDeleteUserAsync(OrganizationUser orgUser);
        Task<AccessPolicyResult> UserCanEditUserTypeAsync(Guid organizationId, OrganizationUserType newType,
            OrganizationUserType? oldType = null);
    }
}
