using System;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.UserInvite
{
    public interface IOrganizationUserInviteAccessPolicies
    {
        AccessPolicyResult CanScale(Organization organization, int seatsToAdd);
        Task<AccessPolicyResult> UserCanEditUserType(Guid organizationId, OrganizationUserType newType, OrganizationUserType? oldType = null);
    }
}
