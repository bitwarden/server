using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrgUser.Invitation
{
    public class OrganizationUserInvitationAccessPolicies : BaseAccessPolicies
    {
        protected readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IPolicyRepository _policyRepository;
        private readonly IUserService _userService;

        public OrganizationUserInvitationAccessPolicies(
            ICurrentContext currentContext,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService,
            IPolicyRepository policyRepository,
            IUserService userService
        )
        {
            _organizationUserRepository = organizationUserRepository;
            _policyRepository = policyRepository;
            _userService = userService;
        }

        protected async Task<AccessPolicyResult> CanBeFreeOrgAdminAsync(Organization org, OrganizationUser orgUser, User user,
            bool adminFailureMessage)
        {
            if (orgUser.Type == OrganizationUserType.Owner || orgUser.Type == OrganizationUserType.Admin)
            {
                if (org.PlanType == PlanType.Free)
                {
                    var adminCount = await _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(
                        user.Id);
                    if (adminCount > 0)
                    {
                        return Fail(string.Concat(
                            adminFailureMessage ? "User" : "You",
                            " can only be an admin of one free organization."
                        ));
                    }
                }
            }

            return Success;
        }

        /// <summary>
        /// Enforces Single Organization and two factory policies of organization user is trying to join
        /// </summary>
        /// <returns></returns>
        protected async Task<AccessPolicyResult> CanJoinOrganizationAsync(User user, OrganizationUser orgUser, bool adminFailureMessage, IEnumerable<OrganizationUser> allOrgUsers = null)
        {
            // Single Org Policy
            allOrgUsers ??= await _organizationUserRepository.GetManyByUserAsync(user.Id);
            var hasOtherOrgs = allOrgUsers.Any(ou => ou.OrganizationId != orgUser.OrganizationId);
            var invitedSingleOrgPolicies = await _policyRepository.GetManyByTypeApplicableToUserIdAsync(user.Id,
                PolicyType.SingleOrg, OrganizationUserStatusType.Invited);

            if (hasOtherOrgs && invitedSingleOrgPolicies.Any(p => p.OrganizationId == orgUser.OrganizationId))
            {
                return adminFailureMessage ?
                    Fail("User is a member of another organization.") :
                    Fail("You cannot join this organization until you enable two-step login on your user account.");
            }

            // Two Factor Authentication Policy
            if (!await _userService.TwoFactorIsEnabledAsync(user))
            {
                var invitedTwoFactorPolicies = await _policyRepository.GetManyByTypeApplicableToUserIdAsync(user.Id,
                    PolicyType.TwoFactorAuthentication, OrganizationUserStatusType.Invited);
                if (invitedTwoFactorPolicies.Any(p => p.OrganizationId == orgUser.OrganizationId))
                {
                    return adminFailureMessage ?
                        Fail("User does not have two-step login enabled.") :
                        Fail("You may not join this organization until you leave or remove all other organizations.");
                }
            }

            return Success;
        }

        /// <summary>
        /// Enforces Single Organization Policy of organizations the users is a member of
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        protected async Task<AccessPolicyResult> CanJoinMoreOrganizationsAsync(User user, bool adminFailureMessage)
        {
            // Enforce Single Organization Policy of other organizations user is a member of
            var singleOrgPolicyCount = await _policyRepository.GetCountByTypeApplicableToUserIdAsync(user.Id,
                PolicyType.SingleOrg);
            if (singleOrgPolicyCount > 0)
            {
                return adminFailureMessage ?
                    Fail("User is a member of another organization that forbids joining more organizations.") :
                    Fail("You cannot join this organization because you are a member of another organization which forbids it");
            }

            return Success;
        }
    }
}
