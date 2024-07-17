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
    //TODO - will remove SecretKey with PM-8107
    public string SecretKey { get; set; }
    //TODO - will remove IntegrationKey with PM-8107
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
                ClientSecret = MaskKey((string)sKey);
                SecretKey = MaskKey((string)sKey);
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
                    ClientSecret = MaskKey((string)clientSecret);
                    SecretKey = MaskKey((string)clientSecret);
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

    /*
    use this method to ensure that both v2 params and v4 params are in sync
    todo will be removed in pm-8107
    */
    private void Temporary_SyncDuoParams()
    {
        // Even if IKey and SKey exist prioritize v4 params ClientId and ClientSecret
        if (!string.IsNullOrWhiteSpace(ClientSecret) && !string.IsNullOrWhiteSpace(ClientId))
        {
            SecretKey = ClientSecret;
            IntegrationKey = ClientId;
        }
        else if (!string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(IntegrationKey))
        {
            ClientSecret = SecretKey;
            ClientId = IntegrationKey;
        }
        else
        {
            throw new InvalidDataException("Invalid Duo parameters.");
        }
    }

    private static string MaskKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        // Mask all but the first 6 characters.
        return string.Concat(key.AsSpan(0, 6), new string('*', key.Length - 6));
    }
}
