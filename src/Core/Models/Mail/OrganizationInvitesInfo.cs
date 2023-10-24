using Bit.Core.Auth.Models.Business;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Models.Mail;
public class OrganizationInvitesInfo
{
    public OrganizationInvitesInfo(
        Organization org,
        bool orgSsoEnabled,
        bool orgSsoLoginRequiredPolicyEnabled,
        IEnumerable<(OrganizationUser orgUser, ExpiringToken token)> invites,
        Dictionary<Guid, bool> orgUserHasExistingUserDict,
        bool initOrganization = false
        )
    {
        OrganizationName = org.Name;
        OrgSsoIdentifier = org.Identifier;

        IsFreeOrg = org.PlanType == PlanType.Free;
        InitOrganization = initOrganization;

        OrgSsoEnabled = orgSsoEnabled;
        OrgSsoLoginRequiredPolicyEnabled = orgSsoLoginRequiredPolicyEnabled;

        Invites = invites;
        OrgUserHasExistingUserDict = orgUserHasExistingUserDict;
    }

    public string OrganizationName { get; }
    public bool IsFreeOrg { get; }
    public bool InitOrganization { get; } = false;
    public bool OrgSsoEnabled { get; }
    public string OrgSsoIdentifier { get; }
    public bool OrgSsoLoginRequiredPolicyEnabled { get; }

    public IEnumerable<(OrganizationUser OrgUser, ExpiringToken Token)> Invites { get; }
    public Dictionary<Guid, bool> OrgUserHasExistingUserDict { get; }

}
