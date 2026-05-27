// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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
        GlobalSettings globalSettings)
    {
        const string freeOrgTitle = "A Bitwarden member invited you to an organization. " +
                                    "Join now to start securing your passwords!";

        var userHasExistingUser = orgInvitesInfo.OrgUserHasExistingUserDict[orgUser.Id];
        var expiringToken = orgInvitesInfo.OrgUserTokenPairs.First(p => p.OrgUser.Id == orgUser.Id).Token;

        return new OrganizationUserInvitedViewModel
        {
            TitleFirst = orgInvitesInfo.IsFreeOrg ? freeOrgTitle : "Join ",
            TitleSecondBold =
                orgInvitesInfo.IsFreeOrg
                    ? string.Empty
                    : CoreHelpers.SanitizeForEmail(orgInvitesInfo.OrganizationName, false),
            TitleThird = orgInvitesInfo.IsFreeOrg ? string.Empty : " on Bitwarden and start securing your passwords!",
            OrganizationName = CoreHelpers.SanitizeForEmail(orgInvitesInfo.OrganizationName, false),
            ExpirationDate =
                $"{expiringToken.ExpirationDate.ToLongDateString()} {expiringToken.ExpirationDate.ToShortTimeString()} UTC",
            WebVaultUrl = globalSettings.BaseServiceUri.VaultWithHash,
            SiteName = globalSettings.SiteName,
            OrgUserHasExistingUser = userHasExistingUser,
            JoinOrganizationButtonText = userHasExistingUser || orgInvitesInfo.IsFreeOrg ? "Accept invitation" : "Finish account setup",
            IsFreeOrg = orgInvitesInfo.IsFreeOrg,
            Url = orgInvitesInfo.GetAcceptUrl(globalSettings.BaseServiceUri.VaultWithHash, orgUser.Id)
        };
    }

    public string OrganizationName { get; set; }
    public string ExpirationDate { get; set; }
    public bool OrgUserHasExistingUser { get; set; }
    public string JoinOrganizationButtonText { get; set; } = "Join Organization";
    public bool IsFreeOrg { get; set; }
    public string Url { get; set; }
}
