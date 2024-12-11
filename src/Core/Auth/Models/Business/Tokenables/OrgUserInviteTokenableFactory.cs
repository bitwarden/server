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
            ExpirationDate = DateTime.UtcNow.Add(
                TimeSpan.FromHours(_globalSettings.OrganizationInviteExpirationHours)
            ),
        };
        return token;
    }
}
