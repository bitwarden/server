using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response.TwoFactor;

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
    public string SecretKey { get; set; }
    public string IntegrationKey { get; set; }

    private void Build(TwoFactorProvider provider)
    {
        if (provider?.MetaData != null && provider.MetaData.Count > 0)
        {
            Enabled = provider.Enabled;

            if (provider.MetaData.ContainsKey("Host"))
            {
                Host = (string)provider.MetaData["Host"];
            }
            if (provider.MetaData.ContainsKey("SKey"))
            {
                SecretKey = (string)provider.MetaData["SKey"];
            }
            if (provider.MetaData.ContainsKey("IKey"))
            {
                IntegrationKey = (string)provider.MetaData["IKey"];
            }
        }
        else
        {
            Enabled = false;
        }
    }
}
