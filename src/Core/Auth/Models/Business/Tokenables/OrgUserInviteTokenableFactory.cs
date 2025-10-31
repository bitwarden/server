using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Settings;

namespace Bit.Core.Auth.Models.Business.Tokenables;

public class OrgUserInviteTokenableFactory : IOrgUserInviteTokenableFactory
{
    private readonly IGlobalSettings _globalSettings;

    public OrgUserInviteTokenableFactory(IGlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public OrgUserInviteTokenable CreateToken(OrganizationUser orgUser)
    {
        var token = new OrgUserInviteTokenable(orgUser)
        {
            ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromHours(_globalSettings.OrganizationInviteExpirationHours))
        };
        return token;
    }

    public OrgUserInviteTokenable CreateToken(OrganizationUser orgUser, string OrganizationDisplayName, PlanType planType)
    {
        var token = new OrgUserInviteTokenable(orgUser)
        {
            ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromHours(_globalSettings.OrganizationInviteExpirationHours)),
            OrgDisplayName = OrganizationDisplayName,
            PlanType = planType
        };
        return token;
    }
}
