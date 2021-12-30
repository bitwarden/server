using System;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation.Accept
{
    public class OrganizationUserAcceptAccessPolicies : OrganizationUserInvitationAccessPolicies, IOrganizationUserAcceptAccessPolicies
    {

        public OrganizationUserAcceptAccessPolicies(
            ICurrentContext currentContext,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService,
            IPolicyRepository policyRepository,
            IUserService userService
        ) : base(currentContext, organizationUserRepository, organizationService, policyRepository, userService)
        {
        }

        public async Task<AccessPolicyResult> CanAcceptInviteAsync(Organization org, User user, OrganizationUser orgUser, bool tokenIsValid)
        {
            if (orgUser == null || org == null)
            {
                return Fail("User invalid.");
            }

            if (!tokenIsValid)
            {
                return Fail("Invalid token.");
            }

            var existingOrgUserCount = await _organizationUserRepository.GetCountByOrganizationAsync(
                orgUser.OrganizationId, user.Email, true);
            if (existingOrgUserCount > 0)
            {
                if (orgUser.Status == OrganizationUserStatusType.Accepted)
                {
                    return Fail("Invitation already accepted. You will receive an email when your organization membership is confirmed.");
                }
                return Fail("You are already part of this organization.");
            }

            if (string.IsNullOrWhiteSpace(orgUser.Email) ||
                !orgUser.Email.Equals(user.Email, StringComparison.InvariantCultureIgnoreCase))
            {
                return Fail("User email does not match invite.");
            }

            return await Success.AndAsync(
                () => CanBeFreeOrgAdminAsync(org, orgUser, user, adminFailureMessage: false),
                () => CanJoinOrganizationAsync(user, orgUser, adminFailureMessage: false),
                () => CanJoinMoreOrganizationsAsync(user, adminFailureMessage: false)
            );
        }
    }
}
