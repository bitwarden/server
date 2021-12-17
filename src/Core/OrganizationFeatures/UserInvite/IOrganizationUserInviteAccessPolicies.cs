using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.UserInvite
{
    public interface IOrganizationUserInviteAccessPolicies
    {

        Task<AccessPolicyResult> CanInviteAsync(Organization organization, IEnumerable<OrganizationUserInviteData> invites, Guid? invitingUserId);
        AccessPolicyResult CanResendInvite(OrganizationUser organizationUser, Organization organization);
        Task<AccessPolicyResult> CanAcceptInviteAsync(Organization organization, User user, OrganizationUser organizationUser,
            bool tokenIsValid);
        Task<AccessPolicyResult> CanConfirmUserAsync(Organization organization, User user, OrganizationUser organizationUser,
            IEnumerable<OrganizationUser> allOrgUsers = null);
    }
}
