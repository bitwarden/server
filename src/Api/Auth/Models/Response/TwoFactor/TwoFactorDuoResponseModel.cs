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
    public string SecretKey { get; set; }
    //TODO - will remove with PM-8107
    public string IntegrationKey { get; set; }
    public string ClientSecret { get; set; }
    public string ClientId { get; set; }

    // updated build to assist in the EDD migration for the Duo 2FA provider
    private void Build(TwoFactorProvider provider)
    {
        if (provider?.MetaData != null && provider.MetaData.Count > 0)
        {
            Enabled = provider.Enabled;

            if (provider.MetaData.TryGetValue("Host", out var host))
            {
                Host = (string)host;
            }
            //todo - will remove SKey and IKey with PM-8107
            // check Skey and IKey first if they exist
            if (provider.MetaData.TryGetValue("SKey", out var sKey))
            {
                ClientSecret = (string)sKey;
                SecretKey = (string)sKey;
            }
            if (provider.MetaData.TryGetValue("IKey", out var iKey))
            {
                IntegrationKey = (string)iKey;
                ClientId = (string)iKey;
            }
            // Even if IKey and SKey exist prioritize v4 params ClientId and ClientSecret
            if (provider.MetaData.TryGetValue("ClientSecret", out var clientSecret))
            {
                if (!string.IsNullOrWhiteSpace((string)clientSecret))
                {
                    ClientSecret = (string)clientSecret;
                    SecretKey = (string)clientSecret;
                }
            }
            if (provider.MetaData.TryGetValue("ClientId", out var clientId))
            {
                if (!string.IsNullOrWhiteSpace((string)clientId))
                {
                    ClientId = (string)clientId;
                    IntegrationKey = (string)clientId;
                }
            }
        }
        else
        {
            Enabled = false;
        }
    }
}
