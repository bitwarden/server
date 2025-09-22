using System.Text;
using Bit.Core.Settings;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Utilities;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess;

/// <summary>
/// This class is used to protect our system from enumeration attacks. This Validator will always return an error result.
/// We hash the SendId Guid passed into the request to select the an error from the list of possible errors. This ensures
/// that the same error is always returned for the same SendId.
/// </summary>
/// <param name="globalSettings">We need access to a hash key to generate the error index.</param>
public class SendNeverAuthenticateRequestValidator(GlobalSettings globalSettings) : ISendAuthenticationMethodValidator<NeverAuthenticate>
{
    private readonly string[] _errorOptions =
    [
        SendAccessConstants.EnumerationProtection.Guid,
        SendAccessConstants.EnumerationProtection.Password,
        SendAccessConstants.EnumerationProtection.Email
    ];

    public Task<GrantValidationResult> ValidateRequestAsync(
        ExtensionGrantValidationContext context,
        NeverAuthenticate authMethod,
        Guid sendId)
    {
        var neverAuthenticateError = GetErrorIndex(sendId, _errorOptions.Length);
        var request = context.Request.Raw;
        var errorType = neverAuthenticateError;

        switch (neverAuthenticateError)
        {
            case SendAccessConstants.EnumerationProtection.Guid:
                errorType = SendAccessConstants.SendIdGuidValidatorResults.InvalidSendId;
                break;
            case SendAccessConstants.EnumerationProtection.Email:
                var hasEmail = request.Get(SendAccessConstants.TokenRequest.Email) is not null;
                errorType = hasEmail ? SendAccessConstants.EmailOtpValidatorResults.EmailInvalid
                    : SendAccessConstants.EmailOtpValidatorResults.EmailRequired;
                break;
            case SendAccessConstants.EnumerationProtection.Password:
                var hasPassword = request.Get(SendAccessConstants.TokenRequest.ClientB64HashedPassword) is not null;
                errorType = hasPassword ? SendAccessConstants.PasswordValidatorResults.RequestPasswordDoesNotMatch
                    : SendAccessConstants.PasswordValidatorResults.RequestPasswordIsRequired;
                break;
        }

        return Task.FromResult(BuildErrorResult(errorType));
    }

    private static GrantValidationResult BuildErrorResult(string errorType)
    {
        // Create error response with custom response data
        var customResponse = new Dictionary<string, object>
        {
            { SendAccessConstants.SendAccessError, errorType }
        };

        var requestError = errorType switch
        {
            SendAccessConstants.EnumerationProtection.Guid => TokenRequestErrors.InvalidGrant,
            SendAccessConstants.PasswordValidatorResults.RequestPasswordIsRequired => TokenRequestErrors.InvalidGrant,
            SendAccessConstants.PasswordValidatorResults.RequestPasswordDoesNotMatch => TokenRequestErrors.InvalidRequest,
            SendAccessConstants.EmailOtpValidatorResults.EmailInvalid => TokenRequestErrors.InvalidGrant,
            SendAccessConstants.EmailOtpValidatorResults.EmailRequired => TokenRequestErrors.InvalidRequest,
            _ => TokenRequestErrors.InvalidGrant
        };

        return new GrantValidationResult(requestError, errorType, customResponse);
    }

    private string GetErrorIndex(Guid sendId, int range)
    {
        var salt = sendId.ToString();
        byte[] hmacKey = [];
        if (CoreHelpers.SettingHasValue(globalSettings.SendDefaultHashKey))
        {
            hmacKey = Encoding.UTF8.GetBytes(globalSettings.SendDefaultHashKey);
        }

        var index = EnumerationProtectionHelpers.GetIndexForInputHash(hmacKey, salt, range);
        return _errorOptions[index];
    }
}
