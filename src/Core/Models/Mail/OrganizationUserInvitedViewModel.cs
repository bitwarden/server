using System.Net;
using Bit.Core.Auth.Models.Business;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Mail;

public class OrganizationUserInvitedViewModel : BaseTitleContactUsMailModel
{

    // Private constructor to enforce usage of the factory method.
    private OrganizationUserInvitedViewModel() { }

    public static OrganizationUserInvitedViewModel CreateFromInviteInfo(
        OrganizationInvitesInfo orgInvitesInfo,
        OrganizationUser orgUser,
        ExpiringToken expiringToken,
        GlobalSettings globalSettings)
    {
        var freeOrgTitle = "A Bitwarden member invited you to an organization. Join now to start securing your passwords!";
        return new OrganizationUserInvitedViewModel
        {
            TitleFirst = orgInvitesInfo.IsFreeOrg ? freeOrgTitle : "Join ",
            TitleSecondBold =
                orgInvitesInfo.IsFreeOrg
                    ? string.Empty
                    : CoreHelpers.SanitizeForEmail(orgInvitesInfo.OrganizationName, false),
            TitleThird = orgInvitesInfo.IsFreeOrg ? string.Empty : " on Bitwarden and start securing your passwords!",
            OrganizationName = CoreHelpers.SanitizeForEmail(orgInvitesInfo.OrganizationName, false) + orgUser.Status,
            Email = WebUtility.UrlEncode(orgUser.Email),
            OrganizationId = orgUser.OrganizationId.ToString(),
            OrganizationUserId = orgUser.Id.ToString(),
            Token = WebUtility.UrlEncode(expiringToken.Token),
            ExpirationDate =
                $"{expiringToken.ExpirationDate.ToLongDateString()} {expiringToken.ExpirationDate.ToShortTimeString()} UTC",
            OrganizationNameUrlEncoded = WebUtility.UrlEncode(orgInvitesInfo.OrganizationName),
            WebVaultUrl = globalSettings.BaseServiceUri.VaultWithHash,
            SiteName = globalSettings.SiteName,
            InitOrganization = orgInvitesInfo.InitOrganization,
            OrgSsoIdentifier = orgInvitesInfo.OrgSsoIdentifier,
            OrgSsoEnabled = orgInvitesInfo.OrgSsoEnabled,
            OrgSsoLoginRequiredPolicyEnabled = orgInvitesInfo.OrgSsoLoginRequiredPolicyEnabled,
            OrgUserHasExistingUser = orgInvitesInfo.OrgUserHasExistingUserDict[orgUser.Id]
        };
    }

    public string OrganizationName { get; set; }
    public string OrganizationId { get; set; }
    public string OrganizationUserId { get; set; }
    public string Email { get; set; }
    public string OrganizationNameUrlEncoded { get; set; }
    public string Token { get; set; }
    public string ExpirationDate { get; set; }
    public bool InitOrganization { get; set; }
    public string OrgSsoIdentifier { get; set; }
    public bool OrgSsoEnabled { get; set; }
    public bool OrgSsoLoginRequiredPolicyEnabled { get; set; }
    public bool OrgUserHasExistingUser { get; set; }

    public string Url
    {
        get
        {
            var baseUrl = $"{WebVaultUrl}/accept-organization";
            var queryParams = new List<string>
            {
                $"organizationId={OrganizationId}",
                $"organizationUserId={OrganizationUserId}",
                $"email={Email}",
                $"organizationName={OrganizationNameUrlEncoded}",
                $"token={Token}",
                $"initOrganization={InitOrganization}",
                $"orgUserHasExistingUser={OrgUserHasExistingUser}"
            };

            if (OrgSsoEnabled && OrgSsoLoginRequiredPolicyEnabled)
            {
                // Only send down the orgSsoIdentifier if we are going to accelerate the user to the SSO login page.
                queryParams.Add($"orgSsoIdentifier={OrgSsoIdentifier}");
            }

            return $"{baseUrl}?{string.Join("&", queryParams)}";
        }
    }
}
