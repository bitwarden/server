using Bit.Core.Auth.IdentityServer;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer.Enums;
using Bit.Identity.IdentityServer.RequestValidators.SendAccess;
using Duende.IdentityModel;

namespace Bit.Identity.IntegrationTest.RequestValidation.SendAccess;

public static class SendAccessTestUtilities
{
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