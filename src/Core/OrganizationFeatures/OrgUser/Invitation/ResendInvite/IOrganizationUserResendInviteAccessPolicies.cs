using Bit.Core.AccessPolicies;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation.ResendInvite
{
    public interface IOrganizationUserResendInviteAccessPolicies
    {

        AccessPolicyResult CanResendInvite(OrganizationUser organizationUser, Organization organization);
    }
}
