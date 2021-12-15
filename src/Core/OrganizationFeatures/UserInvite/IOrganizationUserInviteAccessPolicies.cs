using System;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.UserInvite
{
    public interface IOrganizationUserInviteAccessPolicies
    {
        Task<AccessPolicyResult> UserCanEditUserType(Guid organizationId, OrganizationUserType newType, OrganizationUserType? oldType = null);
        AccessPolicyResult CanResendInvite(OrganizationUser organizationUser, Organization organization);
        Task<AccessPolicyResult> CanAcceptInvite(Organization org, User user, OrganizationUser orgUser, bool tokenIsValid);
    }
}
