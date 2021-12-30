using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation.Invite
{
    public class OrganizationUserInviteAccessPolicies : OrganizationUserAccessPolicies, IOrganizationUserInviteAccessPolicies
    {
        public OrganizationUserInviteAccessPolicies(
            ICurrentContext currentContext,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService
        ) : base(currentContext, organizationUserRepository, organizationService)
        {
        }

        public async Task<AccessPolicyResult> CanInviteAsync(Organization organization, IEnumerable<OrganizationUserInviteData> invites, Guid? invitingUserId)
        {
            if (organization == null || invites.Any(i => i.Emails == null))
            {
                return Fail();
            }

            if (!invitingUserId.HasValue)
            {
                return Success;
            }

            var currentResult = Success;
            foreach (var inviteType in invites.Where(i => i.Type.HasValue).Select(i => i.Type.Value).Distinct())
            {
                if (!currentResult.Permit)
                {
                    break;
                }

                currentResult = currentResult.LazyAnd(await UserCanEditUserTypeAsync(organization.Id, inviteType));
            }

            // validate org has owners
            if (!invites.All(i => i.Type == OrganizationUserType.Owner))
            {
                currentResult = currentResult.LazyAnd(await OrganizationCanLoseOwnerAsync(organization.Id, Array.Empty<OrganizationUser>()));
            }

            return currentResult;
        }
    }
}
