// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Net;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Models.Business;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;

namespace Bit.Core.Models.Mail;

public class OrganizationInvitesInfo
{
    public OrganizationInvitesInfo(
        Organization org,
        bool orgSsoEnabled,
        bool orgSsoLoginRequiredPolicyEnabled,
        IEnumerable<(OrganizationUser orgUser, ExpiringToken token)> orgUserTokenPairs,
        Dictionary<Guid, bool> orgUserHasExistingUserDict,
        bool initOrganization = false,
        string inviterEmail = null
        )
    {
        OrganizationName = org.DisplayName();
        OrgSsoIdentifier = org.Identifier;
        PlanType = org.PlanType;

        IsFreeOrg = org.PlanType == PlanType.Free;
        InitOrganization = initOrganization;

        OrgSsoEnabled = orgSsoEnabled;
        OrgSsoLoginRequiredPolicyEnabled = orgSsoLoginRequiredPolicyEnabled;

        OrgUserTokenPairs = orgUserTokenPairs;
        OrgUserHasExistingUserDict = orgUserHasExistingUserDict;
        InviterEmail = inviterEmail;
    }

    public string OrganizationName { get; }
    public PlanType PlanType { get; }
    public bool IsFreeOrg { get; }
    public bool InitOrganization { get; } = false;
    public bool OrgSsoEnabled { get; }
    public string OrgSsoIdentifier { get; }
    public bool OrgSsoLoginRequiredPolicyEnabled { get; }
    public IEnumerable<(OrganizationUser OrgUser, ExpiringToken Token)> OrgUserTokenPairs { get; }
    public Dictionary<Guid, bool> OrgUserHasExistingUserDict { get; }
    public string InviterEmail { get; }

    public string GetAcceptUrl(string vaultWithHash, Guid orgUserId)
    {
        var pair = OrgUserTokenPairs.First(p => p.OrgUser.Id == orgUserId);
        var orgUser = pair.OrgUser;
        var token = pair.Token.Token;
        var orgUserHasExistingUser = OrgUserHasExistingUserDict[orgUserId];

        var queryParams = new List<string>
        {
            $"organizationId={orgUser.OrganizationId}",
            $"organizationUserId={orgUser.Id}",
            $"email={WebUtility.UrlEncode(orgUser.Email)}",
            $"organizationName={WebUtility.UrlEncode(OrganizationName)}",
            $"token={WebUtility.UrlEncode(token)}",
            $"initOrganization={InitOrganization}",
            $"orgUserHasExistingUser={orgUserHasExistingUser}"
        };

        if (OrgSsoEnabled && OrgSsoLoginRequiredPolicyEnabled)
        {
            // Only send down the orgSsoIdentifier if we are going to accelerate the user to the SSO login page.
            queryParams.Add($"orgSsoIdentifier={OrgSsoIdentifier}");
        }

        return $"{vaultWithHash}/accept-organization?{string.Join("&", queryParams)}";
    }
}
