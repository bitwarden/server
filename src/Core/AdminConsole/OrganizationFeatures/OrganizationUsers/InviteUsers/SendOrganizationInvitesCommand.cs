// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Models.Business;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Mail;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

public class SendOrganizationInvitesCommand(
    IUserRepository userRepository,
    ISsoConfigRepository ssoConfigurationRepository,
    IPolicyRepository policyRepository,
    IOrgUserInviteTokenableFactory orgUserInviteTokenableFactory,
    IDataProtectorTokenFactory<OrgUserInviteTokenable> dataProtectorTokenFactory,
    IMailService mailService) : ISendOrganizationInvitesCommand
{
    public async Task SendInvitesAsync(SendInvitesRequest request)
    {
        var orgInvitesInfo = await BuildOrganizationInvitesInfoAsync(request.Users, request.Organization, request.InitOrganization);

        await mailService.SendOrganizationInviteEmailsAsync(orgInvitesInfo);
    }

    private async Task<OrganizationInvitesInfo> BuildOrganizationInvitesInfoAsync(IEnumerable<OrganizationUser> orgUsers,
        Organization organization, bool initOrganization = false)
    {
        // Materialize the sequence into a list to avoid multiple enumeration warnings
        var orgUsersList = orgUsers.ToList();

        // Email links must include information about the org and user for us to make routing decisions client side
        // Given an org user, determine if existing BW user exists
        var orgUserEmails = orgUsersList.Select(ou => ou.Email).ToList();
        var existingUsers = await userRepository.GetManyByEmailsAsync(orgUserEmails);

        // Create a dictionary to capture both email and id for O(1) lookups
        var existingUserEmailIdDict = existingUsers.ToDictionary(u => u.Email, u => u.Id);

        // Create a dictionary of org user guids and bools for whether or not they have an existing BW user
        var orgUserHasExistingUserDict = orgUsersList.ToDictionary(
            ou => ou.Id,
            ou => ou.Email != null && existingUserEmailIdDict.ContainsKey(ou.Email)
        );

        // Determine if org has SSO enabled and if user is required to login with SSO
        // Note: we only want to call the DB after checking if the org can use SSO per plan and if they have any policies enabled.
        var orgSsoEnabled = organization.UseSso && (await ssoConfigurationRepository.GetByOrganizationIdAsync(organization.Id))?.Enabled == true;
        // Even though the require SSO policy can be turned on regardless of SSO being enabled, for this logic, we only
        // need to check the policy if the org has SSO enabled.
        var orgSsoLoginRequiredPolicyEnabled = orgSsoEnabled &&
                                               organization.UsePolicies &&
                                               (await policyRepository.GetByOrganizationIdTypeAsync(organization.Id, PolicyType.RequireSso))?.Enabled == true;

        // Generate the list of org users and expiring tokens
        // create helper function to create expiring tokens
        (OrganizationUser, ExpiringToken) MakeOrgUserExpiringTokenPair(OrganizationUser orgUser)
        {
            var orgUserInviteTokenable = orgUserInviteTokenableFactory.CreateToken(orgUser);
            var protectedToken = dataProtectorTokenFactory.Protect(orgUserInviteTokenable);
            var associatedUserId = orgUser.Email != null && existingUserEmailIdDict.TryGetValue(orgUser.Email, out var id) ? id : (Guid?)null;
            orgUser.UserId = associatedUserId;
            return (orgUser, new ExpiringToken(protectedToken, orgUserInviteTokenable.ExpirationDate));
        }

        var orgUsersWithExpTokens = orgUsers.Select(MakeOrgUserExpiringTokenPair);

        return new OrganizationInvitesInfo(
            organization,
            orgSsoEnabled,
            orgSsoLoginRequiredPolicyEnabled,
            orgUsersWithExpTokens,
            orgUserHasExistingUserDict,
            initOrganization
        );
    }
}
