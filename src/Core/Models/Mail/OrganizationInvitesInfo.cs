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
        bool initOrganization = false
    )
    {
        OrganizationName = org.DisplayName();
        OrgSsoIdentifier = org.Identifier;

        IsFreeOrg = org.PlanType == PlanType.Free;
        InitOrganization = initOrganization;

        OrgSsoEnabled = orgSsoEnabled;
        OrgSsoLoginRequiredPolicyEnabled = orgSsoLoginRequiredPolicyEnabled;

        OrgUserTokenPairs = orgUserTokenPairs;
        OrgUserHasExistingUserDict = orgUserHasExistingUserDict;
    }

    public string OrganizationName { get; }
    public bool IsFreeOrg { get; }
    public bool InitOrganization { get; } = false;
    public bool OrgSsoEnabled { get; }
    public string OrgSsoIdentifier { get; }
    public bool OrgSsoLoginRequiredPolicyEnabled { get; }

    public IEnumerable<(OrganizationUser OrgUser, ExpiringToken Token)> OrgUserTokenPairs { get; }
    public Dictionary<Guid, bool> OrgUserHasExistingUserDict { get; }
}
