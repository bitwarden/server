// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Net;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationInvite;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Auth.Models.Business;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Models.Mail;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

public class SendOrganizationInvitesCommand(
    IUserRepository userRepository,
    ISsoConfigRepository ssoConfigurationRepository,
    IPolicyQuery policyQuery,
    IOrgUserInviteTokenableFactory orgUserInviteTokenableFactory,
    IDataProtectorTokenFactory<OrgUserInviteTokenable> dataProtectorTokenFactory,
    IMailService mailService,
    IMailer mailer,
    IFeatureService featureService,
    GlobalSettings globalSettings) : ISendOrganizationInvitesCommand
{
    // New user (no existing account) email constants
    private const string _newUserSubject = "set up a Bitwarden account for you";
    private const string _newUserTitle = "set up a Bitwarden password manager account for you.";
    private const string _newUserButton = "Finish account setup";

    // Existing user email constants
    private const string _existingUserSubject = "invited you to their Bitwarden organization";
    private const string _existingUserTitle = "invited you to join them on Bitwarden";
    private const string _existingUserButton = "Accept invitation";

    // Free organization email constants
    private const string _freeOrgNewUserSubject = "You have been invited to Bitwarden Password Manager";
    private const string _freeOrgExistingUserSubject = "You have been invited to a Bitwarden Organization";
    private const string _freeOrgTitle = "You have been invited to Bitwarden Password Manager";

    public async Task SendInvitesAsync(SendInvitesRequest request)
    {
        var orgInvitesInfo = await BuildOrganizationInvitesInfoAsync(request.Users, request.Organization, request.InitOrganization);

        if (featureService.IsEnabled(FeatureFlagKeys.UpdateJoinOrganizationEmailTemplate))
        {
            var inviterEmail = await GetInviterEmailAsync(request.InvitingUserId);
            await SendNewInviteEmailsAsync(orgInvitesInfo, inviterEmail);
        }
        else
        {
            await mailService.SendOrganizationInviteEmailsAsync(orgInvitesInfo);
        }
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

        // hash existing users emails list for O(1) lookups
        var existingUserEmailsHashSet = new HashSet<string>(existingUsers.Select(u => u.Email));

        // Create a dictionary of org user guids and bools for whether or not they have an existing BW user
        var orgUserHasExistingUserDict = orgUsersList.ToDictionary(
            ou => ou.Id,
            ou => existingUserEmailsHashSet.Contains(ou.Email)
        );

        // Determine if org has SSO enabled and if user is required to login with SSO
        // Note: we only want to call the DB after checking if the org can use SSO per plan and if they have any policies enabled.
        var orgSsoEnabled = organization.UseSso && (await ssoConfigurationRepository.GetByOrganizationIdAsync(organization.Id))?.Enabled == true;
        // Even though the require SSO policy can be turned on regardless of SSO being enabled, for this logic, we only
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

    private async Task SendNewInviteEmailsAsync(OrganizationInvitesInfo orgInvitesInfo, string inviterEmail)
    {
        foreach (var (orgUser, token) in orgInvitesInfo.OrgUserTokenPairs)
        {
            var userHasExistingUser = orgInvitesInfo.OrgUserHasExistingUserDict[orgUser.Id];

            await SendInviteEmailAsync(
                userHasExistingUser,
                orgInvitesInfo,
                orgUser,
                token,
                inviterEmail
            );
        }
    }

    private async Task SendInviteEmailAsync(
        bool userHasExistingUser,
        OrganizationInvitesInfo orgInvitesInfo,
        OrganizationUser orgUser,
        ExpiringToken token,
        string inviterEmail)
    {
        if (PlanConstants.EnterprisePlanTypes.Contains(orgInvitesInfo.PlanType) ||
            PlanConstants.TeamsPlanTypes.Contains(orgInvitesInfo.PlanType) ||
            orgInvitesInfo.PlanType == PlanType.TeamsStarter ||
            orgInvitesInfo.PlanType == PlanType.TeamsStarter2023 ||
            orgInvitesInfo.PlanType == PlanType.Custom)
        {
            if (userHasExistingUser)
            {
                await SendEnterpriseTeamsExistingUserInviteAsync(orgInvitesInfo, orgUser, token, inviterEmail);
            }
            else
            {
                await SendEnterpriseTeamsNewUserInviteAsync(orgInvitesInfo, orgUser, token, inviterEmail);
            }
        }
        else if (PlanConstants.FamiliesPlanTypes.Contains(orgInvitesInfo.PlanType))
        {
            if (userHasExistingUser)
            {
                await SendFamiliesExistingUserInviteAsync(orgInvitesInfo, orgUser, token, inviterEmail);
            }
            else
            {
                await SendFamiliesNewUserInviteAsync(orgInvitesInfo, orgUser, token, inviterEmail);
            }
        }
        else
        {
            // Free plan (default)
            await SendFreeOrganizationInviteAsync(orgInvitesInfo, orgUser, token, inviterEmail, userHasExistingUser);
        }
    }

    private async Task SendEnterpriseTeamsNewUserInviteAsync(
        OrganizationInvitesInfo orgInvitesInfo, OrganizationUser orgUser, ExpiringToken token, string inviterEmail)
    {
        var organizationName = WebUtility.HtmlDecode(orgInvitesInfo.OrganizationName);
        var mail = new OrganizationInviteEnterpriseTeamsNewUser
        {
            ToEmails = [orgUser.Email],
            Subject = $"{organizationName} {_newUserSubject}",
            View = CreateEnterpriseTeamsNewUserView(orgInvitesInfo, orgUser, token, inviterEmail)
        };
        await mailer.SendEmail(mail);
    }

    private async Task SendEnterpriseTeamsExistingUserInviteAsync(
        OrganizationInvitesInfo orgInvitesInfo, OrganizationUser orgUser, ExpiringToken token, string inviterEmail)
    {
        var organizationName = WebUtility.HtmlDecode(orgInvitesInfo.OrganizationName);
        var mail = new OrganizationInviteEnterpriseTeamsExistingUser
        {
            ToEmails = [orgUser.Email],
            Subject = $"{organizationName} {_existingUserSubject}",
            View = CreateEnterpriseTeamsExistingUserView(orgInvitesInfo, orgUser, token, inviterEmail)
        };
        await mailer.SendEmail(mail);
    }

    private async Task SendFamiliesNewUserInviteAsync(
        OrganizationInvitesInfo orgInvitesInfo, OrganizationUser orgUser, ExpiringToken token, string inviterEmail)
    {
        var organizationName = WebUtility.HtmlDecode(orgInvitesInfo.OrganizationName);
        var mail = new OrganizationInviteFamiliesNewUser
        {
            ToEmails = [orgUser.Email],
            Subject = $"{organizationName} {_newUserSubject}",
            View = CreateFamiliesNewUserView(orgInvitesInfo, orgUser, token, inviterEmail)
        };
        await mailer.SendEmail(mail);
    }

    private async Task SendFamiliesExistingUserInviteAsync(
        OrganizationInvitesInfo orgInvitesInfo, OrganizationUser orgUser, ExpiringToken token, string inviterEmail)
    {
        var organizationName = WebUtility.HtmlDecode(orgInvitesInfo.OrganizationName);
        var mail = new OrganizationInviteFamiliesExistingUser
        {
            ToEmails = [orgUser.Email],
            Subject = $"{organizationName} {_existingUserSubject}",
            View = CreateFamiliesExistingUserView(orgInvitesInfo, orgUser, token, inviterEmail)
        };
        await mailer.SendEmail(mail);
    }

    private async Task SendFreeOrganizationInviteAsync(
        OrganizationInvitesInfo orgInvitesInfo, OrganizationUser orgUser, ExpiringToken token, string inviterEmail, bool userHasExistingUser)
    {
        var mail = new OrganizationInviteFree
        {
            ToEmails = [orgUser.Email],
            Subject = userHasExistingUser ? _freeOrgExistingUserSubject : _freeOrgNewUserSubject,
            View = CreateFreeView(orgInvitesInfo, orgUser, token, inviterEmail)
        };
        await mailer.SendEmail(mail);
    }

    private OrganizationInviteEnterpriseTeamsNewUserView CreateEnterpriseTeamsNewUserView(
        OrganizationInvitesInfo orgInvitesInfo, OrganizationUser orgUser, ExpiringToken token, string inviterEmail)
    {
        var organizationName = WebUtility.HtmlDecode(orgInvitesInfo.OrganizationName);
        return new OrganizationInviteEnterpriseTeamsNewUserView
        {
            OrganizationName = organizationName,
            Email = orgUser.Email,
            ExpirationDate = FormatExpirationDate(token.ExpirationDate),
            Url = BuildInvitationUrl(orgInvitesInfo, orgUser, token),
            ButtonText = _newUserButton,
            InviterEmail = inviterEmail
        };
    }

    private OrganizationInviteEnterpriseTeamsExistingUserView CreateEnterpriseTeamsExistingUserView(
        OrganizationInvitesInfo orgInvitesInfo, OrganizationUser orgUser, ExpiringToken token, string inviterEmail)
    {
        var organizationName = WebUtility.HtmlDecode(orgInvitesInfo.OrganizationName);
        return new OrganizationInviteEnterpriseTeamsExistingUserView
        {
            OrganizationName = organizationName,
            Email = orgUser.Email,
            ExpirationDate = FormatExpirationDate(token.ExpirationDate),
            Url = BuildInvitationUrl(orgInvitesInfo, orgUser, token),
            ButtonText = _existingUserButton,
            InviterEmail = inviterEmail
        };
    }

    private OrganizationInviteFamiliesNewUserView CreateFamiliesNewUserView(
        OrganizationInvitesInfo orgInvitesInfo, OrganizationUser orgUser, ExpiringToken token, string inviterEmail)
    {
        var organizationName = WebUtility.HtmlDecode(orgInvitesInfo.OrganizationName);
        return new OrganizationInviteFamiliesNewUserView
        {
            OrganizationName = organizationName,
            Email = orgUser.Email,
            ExpirationDate = FormatExpirationDate(token.ExpirationDate),
            Url = BuildInvitationUrl(orgInvitesInfo, orgUser, token),
            ButtonText = _newUserButton,
            InviterEmail = inviterEmail
        };
    }

    private OrganizationInviteFamiliesExistingUserView CreateFamiliesExistingUserView(
        OrganizationInvitesInfo orgInvitesInfo, OrganizationUser orgUser, ExpiringToken token, string inviterEmail)
    {
        var organizationName = WebUtility.HtmlDecode(orgInvitesInfo.OrganizationName);
        return new OrganizationInviteFamiliesExistingUserView
        {
            OrganizationName = organizationName,
            Email = orgUser.Email,
            ExpirationDate = FormatExpirationDate(token.ExpirationDate),
            Url = BuildInvitationUrl(orgInvitesInfo, orgUser, token),
            ButtonText = _existingUserButton,
            InviterEmail = inviterEmail
        };
    }

    private OrganizationInviteFreeView CreateFreeView(
        OrganizationInvitesInfo orgInvitesInfo, OrganizationUser orgUser, ExpiringToken token, string inviterEmail)
    {
        var organizationName = WebUtility.HtmlDecode(orgInvitesInfo.OrganizationName);
        return new OrganizationInviteFreeView
        {
            OrganizationName = organizationName,
            Email = orgUser.Email,
            ExpirationDate = FormatExpirationDate(token.ExpirationDate),
            Url = BuildInvitationUrl(orgInvitesInfo, orgUser, token),
            ButtonText = _existingUserButton,
            InviterEmail = inviterEmail
        };
    }

    private string BuildInvitationUrl(OrganizationInvitesInfo orgInvitesInfo, OrganizationUser orgUser, ExpiringToken token)
    {
        var baseUrl = $"{globalSettings.BaseServiceUri.VaultWithHash}/accept-organization";
        var queryParams = new List<string>
        {
            $"organizationId={orgUser.OrganizationId}",
            $"organizationUserId={orgUser.Id}",
            $"email={WebUtility.UrlEncode(orgUser.Email)}",
            $"organizationName={WebUtility.UrlEncode(orgInvitesInfo.OrganizationName)}",
            $"token={WebUtility.UrlEncode(token.Token)}",
            $"initOrganization={orgInvitesInfo.InitOrganization}",
            $"orgUserHasExistingUser={orgInvitesInfo.OrgUserHasExistingUserDict[orgUser.Id]}"
        };

        if (orgInvitesInfo.OrgSsoEnabled && orgInvitesInfo.OrgSsoLoginRequiredPolicyEnabled)
        {
            queryParams.Add($"orgSsoIdentifier={orgInvitesInfo.OrgSsoIdentifier}");
        }

        return $"{baseUrl}?{string.Join("&", queryParams)}";
    }

    private async Task<string> GetInviterEmailAsync(Guid? invitingUserId)
    {
        if (!invitingUserId.HasValue)
        {
            return null;
        }

        var invitingUser = await userRepository.GetByIdAsync(invitingUserId.Value);
        return invitingUser?.Email;
    }

    private static string FormatExpirationDate(DateTime expirationDate) =>
        $"{expirationDate.ToLongDateString()} {expirationDate.ToShortTimeString()} UTC";
}
