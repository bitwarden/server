using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response.TwoFactor;

public class TwoFactorProviderResponseModel : ResponseModel
{
    private const string ResponseObj = "twoFactorProvider";

    public TwoFactorProviderResponseModel(TwoFactorProviderType type, TwoFactorProvider provider)
        : base(ResponseObj)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        Enabled = provider.Enabled;
        Type = type;
    }

    public TwoFactorProviderResponseModel(TwoFactorProviderType type, User user)
        : base(ResponseObj)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        var provider = user.GetTwoFactorProvider(type);
        Enabled = provider?.Enabled ?? false;
        Type = type;
    }

    public TwoFactorProviderResponseModel(TwoFactorProviderType type, Organization organization)
        : base(ResponseObj)
    {
        if (organization == null)
        {
            throw new ArgumentNullException(nameof(organization));
        }

        var provider = organization.GetTwoFactorProvider(type);
        Enabled = provider?.Enabled ?? false;
        Type = type;
    }

    public bool Enabled { get; set; }
    public TwoFactorProviderType Type { get; set; }
}
