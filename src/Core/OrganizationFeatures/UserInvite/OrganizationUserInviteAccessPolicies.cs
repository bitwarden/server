using System;
using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.AccessPolicies;
using Bit.Core.Repositories;
using System.Linq;
using Bit.Core.Services;
using System.Collections.Generic;
using Bit.Core.OrganizationFeatures.OrgUser;
using Bit.Core.Models.Data;

namespace Bit.Core.OrganizationFeatures.UserInvite
{
    public class OrganizationUserInviteAccessPolicies : OrganizationUserAccessPolicies, IOrganizationUserInviteAccessPolicies
    {
        private ICurrentContext _currentContext;
        private IOrganizationUserRepository _organizationUserRepository;
        private IPolicyRepository _policyRepository;
        private IUserService _userService;

        public OrganizationUserInviteAccessPolicies(
            ICurrentContext currentContext,
            IOrganizationUserRepository organizationUserRepository,
            IOrganizationService organizationService,
            IPolicyRepository policyRepository,
            IUserService userService
        ) : base(currentContext, organizationUserRepository, organizationService)
        {
            _currentContext = currentContext;
            _organizationUserRepository = organizationUserRepository;
            _policyRepository = policyRepository;
            _userService = userService;
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

        public async Task<AccessPolicyResult> CanConfirmUserAsync(Organization org, User user, OrganizationUser orgUser, IEnumerable<OrganizationUser> allOrgUsers)
        {
            return await Success.AndAsync(
                () => CanBeFreeOrgAdminAsync(org, orgUser, user, adminFailureMessage: true),
                () => CanJoinOrganizationAsync(user, orgUser, adminFailureMessage: true, allOrgUsers),
                () => CanJoinMoreOrganizationsAsync(user, adminFailureMessage: true)
            );
        }

        private async Task<AccessPolicyResult> CanBeFreeOrgAdminAsync(Organization org, OrganizationUser orgUser, User user,
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
        private async Task<AccessPolicyResult> CanJoinOrganizationAsync(User user, OrganizationUser orgUser, bool adminFailureMessage, IEnumerable<OrganizationUser> allOrgUsers = null)
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
        private async Task<AccessPolicyResult> CanJoinMoreOrganizationsAsync(User user, bool adminFailureMessage)
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
