using Bit.Core.Auth.Enums;
using Bit.Core.Entities;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Email provider details. Hydrated from <see cref="User"/>; embedded by the
/// per-action <c>TwoFactorEmail*ResponseModel</c> wrappers.
/// </summary>
public class TwoFactorEmailDetails
{
    public TwoFactorEmailDetails(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

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
    public string? Email { get; set; }
}
