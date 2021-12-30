using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Context;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation.Confirm
{
    public class OrganizationUserConfirmAccessPolicies : OrganizationUserInvitationAccessPolicies, IOrganizationUserConfirmAccessPolicies
    {
        public OrganizationUserConfirmAccessPolicies(
            ICurrentContext currentContext,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService,
            IPolicyRepository policyRepository,
            IUserService userService
        ) : base(currentContext, organizationUserRepository, organizationService, policyRepository, userService)
        {
        }

        public async Task<AccessPolicyResult> CanConfirmUserAsync(Organization org, User user, OrganizationUser orgUser, IEnumerable<OrganizationUser> allOrgUsers)
        {
            return await Success.AndAsync(
                () => CanBeFreeOrgAdminAsync(org, orgUser, user, adminFailureMessage: true),
                () => CanJoinOrganizationAsync(user, orgUser, adminFailureMessage: true, allOrgUsers),
                () => CanJoinMoreOrganizationsAsync(user, adminFailureMessage: true)
            );
        }
    }
}
