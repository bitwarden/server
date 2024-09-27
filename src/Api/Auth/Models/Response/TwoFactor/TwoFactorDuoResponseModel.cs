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
        ArgumentNullException.ThrowIfNull(user);

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
        Build(provider);
    }

    public TwoFactorDuoResponseModel(Organization org)
        : base(ResponseObj)
    {
        ArgumentNullException.ThrowIfNull(org);

        var provider = org.GetTwoFactorProvider(TwoFactorProviderType.OrganizationDuo);
        Build(provider);
    }

    public bool Enabled { get; set; }
    public string Host { get; set; }
    public string ClientSecret { get; set; }
    public string ClientId { get; set; }

    private void Build(TwoFactorProvider provider)
    {
        if (provider?.MetaData != null && provider.MetaData.Count > 0)
        {
            Enabled = provider.Enabled;

            if (provider.MetaData.TryGetValue("Host", out var host))
            {
                Host = (string)host;
            }
            if (provider.MetaData.TryGetValue("ClientSecret", out var clientSecret))
            {
                ClientSecret = MaskSecret((string)clientSecret);
            }
            if (provider.MetaData.TryGetValue("ClientId", out var clientId))
            {
                ClientId = (string)clientId;
            }
        }
        else
        {
            Enabled = false;
        }
    }

    private static string MaskSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length <= 6)
        {
            return secret;
        }

        // Mask all but the first 6 characters.
        return string.Concat(secret.AsSpan(0, 6), new string('*', secret.Length - 6));
    }
}
