// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using OtpNet;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Authenticator provider details. Hydrated from <see cref="User"/>; embedded by the
/// per-action <c>TwoFactorAuthenticator*ResponseModel</c> wrappers.
/// </summary>
public class TwoFactorAuthenticatorDetails
{
    public TwoFactorAuthenticatorDetails(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Authenticator);
        if (provider?.MetaData?.TryGetValue("Key", out var keyValue) ?? false)
        {
            Key = (string)keyValue;
            Enabled = provider.Enabled;
        }
        else
        {
            // No existing provider — mint a fresh key so the client can render the QR code.
            var key = KeyGeneration.GenerateRandomKey(20);
            Key = Base32Encoding.ToString(key);
            Enabled = false;
        }
    }

    public bool Enabled { get; set; }
    public string Key { get; set; }
}
