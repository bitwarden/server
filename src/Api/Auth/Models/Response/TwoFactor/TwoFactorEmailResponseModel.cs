using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

public class TwoFactorEmailResponseModel : ResponseModel
{
    public TwoFactorEmailResponseModel(User user)
        : base("twoFactorEmail")
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
        if (provider?.MetaData?.TryGetValue("Email", out var email) ?? false)
        {
            Email = (string)email;
            Enabled = provider.Enabled;
        }
        else
        {
            Enabled = false;
        }
    }

    public bool Enabled { get; set; }
    public string Email { get; set; }
}
