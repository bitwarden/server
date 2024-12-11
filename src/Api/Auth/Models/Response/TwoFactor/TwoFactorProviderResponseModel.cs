using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

public class TwoFactorProviderResponseModel : ResponseModel
{
    private const string ResponseObj = "twoFactorProvider";

    public TwoFactorProviderResponseModel(TwoFactorProviderType type, TwoFactorProvider provider)
        : base(ResponseObj)
    {
        ArgumentNullException.ThrowIfNull(provider);

        Enabled = provider.Enabled;
        Type = type;
    }

    public TwoFactorProviderResponseModel(TwoFactorProviderType type, User user)
        : base(ResponseObj)
    {
        ArgumentNullException.ThrowIfNull(user);

        var provider = user.GetTwoFactorProvider(type);
        Enabled = provider?.Enabled ?? false;
        Type = type;
    }

    public TwoFactorProviderResponseModel(TwoFactorProviderType type, Organization organization)
        : base(ResponseObj)
    {
        ArgumentNullException.ThrowIfNull(organization);

        var provider = organization.GetTwoFactorProvider(type);
        Enabled = provider?.Enabled ?? false;
        Type = type;
    }

    public bool Enabled { get; set; }
    public TwoFactorProviderType Type { get; set; }
}
