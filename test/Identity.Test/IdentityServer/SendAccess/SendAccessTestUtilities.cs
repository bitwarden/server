using System.Collections.Specialized;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer.Enums;
using Bit.Identity.IdentityServer.RequestValidators.SendAccess;
using Duende.IdentityModel;

namespace Bit.Identity.Test.IdentityServer.SendAccess;

public static class SendAccessTestUtilities
{
    /// <summary>
    /// Builds the unique identifier the email OTP is cached under, independently of the validator, so an
    /// unintended change to its format fails these tests instead of passing silently.
    /// </summary>
    public static string ExpectedOtpUniqueIdentifier(Guid sendId, string email, string deviceIdentifier)
    {
        var deviceIdentifierHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(deviceIdentifier)));
        return string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2}", sendId, email, deviceIdentifierHash);
    }

    public static NameValueCollection CreateValidatedTokenRequest(
        Guid sendId,
        string sendEmail = null,
        string otpCode = null,
        params string[] passwordHash)
    {
        var sendIdBase64 = CoreHelpers.Base64UrlEncode(sendId.ToByteArray());

        var rawRequestParameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, CustomGrantTypes.SendAccess },
            { OidcConstants.TokenRequest.ClientId, BitwardenClient.Send },
            { OidcConstants.TokenRequest.Scope, ApiScopes.ApiSendAccess },
            { "device_type", ((int)DeviceType.FirefoxBrowser).ToString() },
            { SendAccessConstants.TokenRequest.SendId, sendIdBase64 }
        };

        if (sendEmail != null)
        {
            rawRequestParameters.Add(SendAccessConstants.TokenRequest.Email, sendEmail);
        }

        if (otpCode != null && sendEmail != null)
        {
            rawRequestParameters.Add(SendAccessConstants.TokenRequest.Otp, otpCode);
        }

        if (passwordHash != null && passwordHash.Length > 0)
        {
            foreach (var hash in passwordHash)
            {
                rawRequestParameters.Add(SendAccessConstants.TokenRequest.ClientB64HashedPassword, hash);
            }
        }

        return rawRequestParameters;
    }
}
