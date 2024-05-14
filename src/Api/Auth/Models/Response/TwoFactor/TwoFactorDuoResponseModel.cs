using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

public class TwoFactorDuoResponseModel : ResponseModel
{
    private const string ResponseObj = "twoFactorDuo";

    public TwoFactorDuoResponseModel(User user)
        : base(ResponseObj)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
        Build(provider);
    }

    public TwoFactorDuoResponseModel(Organization org)
        : base(ResponseObj)
    {
        if (org == null)
        {
            throw new ArgumentNullException(nameof(org));
        }

        var provider = org.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo);
        Build(provider);
    }

    public bool Enabled { get; set; }
    public string Host { get; set; }
    //TODO - will remove with PM-8107
    public string SKey { get; set; }
    //TODO - will remove with PM-8107
    public string IKey { get; set; }
    public string ClientSecret { get; set; }
    public string ClientId { get; set; }

    // updated build to assist in the EDD migration for the Duo 2FA provider
    private void Build(TwoFactorProvider provider)
    {
        if (provider?.MetaData != null && provider.MetaData.Count > 0)
        {
            Enabled = provider.Enabled;

            if (provider.MetaData.ContainsKey("Host"))
            {
                Host = (string)provider.MetaData["Host"];
            }
            //todo - will remove SKey and IKey with PM-8107
            if (provider.MetaData.ContainsKey("SKey"))
            {
                ClientSecret = (string)provider.MetaData["SKey"];
                SKey = (string)provider.MetaData["SKey"];
            }
            if (provider.MetaData.ContainsKey("IKey"))
            {
                IKey = (string)provider.MetaData["IKey"];
                ClientId = (string)provider.MetaData["IKey"];
            }
            if (provider.MetaData.ContainsKey("ClientSecret"))
            {
                ClientSecret = (string)provider.MetaData["ClientSecret"];
                SKey = (string)provider.MetaData["ClientSecret"];

            }
            if (provider.MetaData.ContainsKey("ClientId"))
            {
                ClientId = (string)provider.MetaData["ClientId"];
                IKey = (string)provider.MetaData["ClientId"];
            }
        }
        else
        {
            Enabled = false;
        }
    }
}
