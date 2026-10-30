using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer.Enums;
using Bit.Identity.IdentityServer.RequestValidators.SendAccess;
using Bit.IntegrationTestCommon.Factories;
using Duende.IdentityModel;

namespace Bit.Identity.IntegrationTest.RequestValidation.SendAccess;

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

    /// <summary>A valid device identifier for tests that are not about the identifier itself.</summary>
    public const string DeviceIdentifier = IdentityApplicationFactory.DefaultDeviceIdentifier;

    public static FormUrlEncodedContent CreateTokenRequestBody(
        Guid sendId,
        string email = null,
        string emailOtp = null,
        string password = null)
    {
        var sendIdBase64 = CoreHelpers.Base64UrlEncode(sendId.ToByteArray());
        var parameters = new List<KeyValuePair<string, string>>
        {
            new(OidcConstants.TokenRequest.GrantType, CustomGrantTypes.SendAccess),
            new(OidcConstants.TokenRequest.ClientId, BitwardenClient.Send),
            new(SendAccessConstants.TokenRequest.SendId, sendIdBase64),
            new(OidcConstants.TokenRequest.Scope, ApiScopes.ApiSendAccess),
            new("device_type", "10")
        };

        if (!string.IsNullOrEmpty(email))
        {
            parameters.Add(new KeyValuePair<string, string>(SendAccessConstants.TokenRequest.Email, email));
        }

        if (!string.IsNullOrEmpty(emailOtp))
        {
            parameters.Add(new KeyValuePair<string, string>(SendAccessConstants.TokenRequest.Otp, emailOtp));
        }

        if (!string.IsNullOrEmpty(password))
        {
            parameters.Add(new KeyValuePair<string, string>(SendAccessConstants.TokenRequest.ClientB64HashedPassword, password));
        }

        return new FormUrlEncodedContent(parameters);
    }
}
