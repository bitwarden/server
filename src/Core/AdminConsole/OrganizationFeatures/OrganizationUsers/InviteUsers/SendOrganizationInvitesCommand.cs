// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Utilities.DebuggingInstruments;
using Bit.Core.Auth.Models.Business;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Models.Mail;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

public class SendOrganizationInvitesCommand(
    IUserRepository userRepository,
    IOrganizationUserRepository organizationUserRepository,
    ISsoConfigRepository ssoConfigurationRepository,
    IPolicyQuery policyQuery,
    IOrgUserInviteTokenableFactory orgUserInviteTokenableFactory,
    IDataProtectorTokenFactory<OrgUserInviteTokenable> dataProtectorTokenFactory,
    IMailService mailService,
    ILogger<SendOrganizationInvitesCommand> logger) : ISendOrganizationInvitesCommand
{
    public async Task SendInvitesAsync(SendInvitesRequest request)
    {
        var (orgUsers, orgUserEmails) = await RepairAndFilterUsersWithoutEmailAsync(request.Users);
        if (orgUsers.Count == 0)
        {
            return;
        }

        var inviterEmail = await GetInviterEmailAsync(request.InvitingUserId);
        var orgInvitesInfo = await BuildOrganizationInvitesInfoAsync(
            orgUsers, orgUserEmails, request.Organization, request.InitOrganization, inviterEmail);
        await mailService.SendUpdatedOrganizationInviteEmailsAsync(orgInvitesInfo);
    }

    /// <summary>
    /// Self-heals invited org users missing their email, then returns the users and emails that can be invited.
    /// </summary>
    /// <remarks>
    /// SSO JIT provisioning can leave a corrupt invited row (Email null, UserId populated). The email must be
    /// persisted, not just patched in memory, because the invite token is re-validated against the stored email at
    /// accept time. When the row has a UserId we recover the email from that user, null the UserId to restore the
    /// canonical invited shape, and persist the repair. Rows that cannot be repaired are logged and dropped.
    /// </remarks>
    private async Task<(List<OrganizationUser> OrgUsers, List<string> Emails)> RepairAndFilterUsersWithoutEmailAsync(
        OrganizationUser[] requestedOrgUsers)
    {
        var missingEmail = requestedOrgUsers.Where(ou => string.IsNullOrWhiteSpace(ou.Email)).ToList();

        if (missingEmail.Count == 0)
        {
            return (requestedOrgUsers.ToList(), requestedOrgUsers.Select(ou => ou.Email).ToList());
        }

        var linkedUsersById = (await userRepository.GetManyAsync(
                missingEmail.Where(ou => ou.UserId.HasValue).Select(ou => ou.UserId.Value)))
            .ToDictionary(user => user.Id);

        var repaired = new List<OrganizationUser>();
        var orgUsers = new List<OrganizationUser>();
        var emails = new List<string>();

        foreach (var orgUser in requestedOrgUsers)
        {
            if (!string.IsNullOrWhiteSpace(orgUser.Email))
            {
                orgUsers.Add(orgUser);
                emails.Add(orgUser.Email);
                continue;
            }

            if (orgUser.UserId.HasValue &&
                linkedUsersById.TryGetValue(orgUser.UserId.Value, out var linkedUser) &&
                !string.IsNullOrWhiteSpace(linkedUser.Email))
            {
                orgUser.Email = linkedUser.Email;
                repaired.Add(orgUser);
                orgUsers.Add(orgUser);
                emails.Add(orgUser.Email);
                continue;
            }

            logger.LogUserInviteStateDiagnostics(orgUser);
        }

        if (repaired.Count != 0)
        {
            await organizationUserRepository.ReplaceManyAsync(repaired);
        }

        return (orgUsers, emails);
    }

    private async Task<OrganizationInvitesInfo> BuildOrganizationInvitesInfoAsync(List<OrganizationUser> orgUsers,
        List<string> orgUserEmails, Organization organization, bool initOrganization, string inviterEmail)
    {
        // Email links must include information about the org and user for us to make routing decisions client side
        // Given an org user, determine if existing BW user exists
        var existingUsers = await userRepository.GetManyByEmailsAsync(orgUserEmails);

        // hash existing users emails list for O(1) lookups
        var existingUserEmailsHashSet = new HashSet<string>(existingUsers.Select(u => u.Email),
            StringComparer.OrdinalIgnoreCase);

        // Create a dictionary of org user guids and bools for whether they have an existing BW user
        var orgUserHasExistingUserDict = orgUsers.ToDictionary(
            ou => ou.Id,
            ou => existingUserEmailsHashSet.Contains(ou.Email)
        );

        // Determine if org has SSO enabled and if user is required to log in with SSO
        // Note: we only want to call the DB after checking if the org can use SSO per plan and if they have any policies enabled.
        var orgSsoEnabled = organization.UseSso && (await ssoConfigurationRepository.GetByOrganizationIdAsync(organization.Id))?.Enabled == true;
        // Even though the Require SSO policy can be turned on regardless of SSO being enabled, for this logic, we only
        // need to check the policy if the org has SSO enabled.
        var orgSsoLoginRequiredPolicyEnabled = orgSsoEnabled &&
                                               organization.UsePolicies &&
                                               (await policyQuery.RunAsync(organization.Id, PolicyType.RequireSso)).Enabled;

        // Generate the list of org users and expiring tokens
        // create helper function to create expiring tokens
        (OrganizationUser, ExpiringToken) MakeOrgUserExpiringTokenPair(OrganizationUser orgUser)
        {
            var orgUserInviteTokenable = orgUserInviteTokenableFactory.CreateToken(orgUser);
            var protectedToken = dataProtectorTokenFactory.Protect(orgUserInviteTokenable);
            return (orgUser, new ExpiringToken(protectedToken, orgUserInviteTokenable.ExpirationDate));
        }

        // Materialized so that consumers enumerating more than once do not mint a new token each time
        var orgUsersWithExpTokens = orgUsers.Select(MakeOrgUserExpiringTokenPair).ToList();

        return new OrganizationInvitesInfo(
            organization,
            orgSsoEnabled,
            orgSsoLoginRequiredPolicyEnabled,
            orgUsersWithExpTokens,
            orgUserHasExistingUserDict,
            initOrganization,
            inviterEmail
        );
    }

    private async Task<string> GetInviterEmailAsync(Guid? invitingUserId)
    {
        if (!invitingUserId.HasValue || invitingUserId.Value == Guid.Empty)
        {
            return null;
        }

        var invitingUser = await userRepository.GetByIdAsync(invitingUserId.Value);
        return invitingUser?.Email;
    }
}
