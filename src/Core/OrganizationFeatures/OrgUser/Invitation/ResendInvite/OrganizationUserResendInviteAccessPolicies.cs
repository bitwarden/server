using Bit.Core.AccessPolicies;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation.ResendInvite
{
    public class OrganizationUserResendInviteAccessPolicies : BaseAccessPolicies, IOrganizationUserResendInviteAccessPolicies
    {
        public OrganizationUserResendInviteAccessPolicies(
        )
        {
        }

        public AccessPolicyResult CanResendInvite(OrganizationUser organizationUser, Organization organization)
        {
            if (organizationUser == null)
            {
                return Fail();
            }

            if (organizationUser.Status != OrganizationUserStatusType.Invited || organizationUser.OrganizationId != organization.Id)
            {
                return Fail("User Invalid.");
            }

            return Success;
        }
    }
}
