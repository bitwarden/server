using System.Security.Claims;
using Bit.Core;
using Bit.Core.Identity;
using Bit.Core.Services;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer.RequestValidators.SendAccess.Enums;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess;

public class SendAccessGrantValidator(
    ISendAuthenticationQuery _sendAuthenticationQuery,
    ISendPasswordRequestValidator _sendPasswordRequestValidator,
    IFeatureService _featureService)
: IExtensionGrantValidator
{
    public const string GrantType = "send_access";

    string IExtensionGrantValidator.GrantType => GrantType;

    private static readonly Dictionary<SendGrantValidatorResultTypes, string>
    _sendGrantValidatorErrors = new()
    {
        { SendGrantValidatorResultTypes.MissingSendId, "send_id is required." },
        { SendGrantValidatorResultTypes.InvalidRequest, "Invalid request." }
    };


    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        // Check the feature flag
        if (!_featureService.IsEnabled(FeatureFlagKeys.SendAuthorization))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant);
        }
        var sendIdGuid = GetRequestSendId(context);
        if (sendIdGuid == Guid.Empty)
        {
            // Validation failed, result is already set in ValidateRequest
            return;
        }

        // Look up send by id
        var method = await _sendAuthenticationQuery.GetAuthenticationMethod(sendIdGuid);

        switch (method)
        {
            case NeverAuthenticate:
                // null send scenario.
                // TODO: Add send enumeration protection here (primarily benefits self hosted instances).
                // We should only map to password or email + OTP protected. If user submits password guess for a
                // falsely protected send, then we will return invalid password.
                // TODO: we should re-use _invalidGrantPasswordInvalid or similar error message here.
                context.Result = new GrantValidationResult(
                    TokenRequestErrors.InvalidRequest,
                    _sendGrantValidatorErrors[SendGrantValidatorResultTypes.InvalidRequest]);
                return;

            case NotAuthenticated:
                // automatically issue access token
                context.Result = BuildBaseSuccessResult(sendIdGuid);
                return;

            case ResourcePassword rp:
                var passwordValid = _sendPasswordRequestValidator.ValidateSendPassword(context, rp);
                if (!passwordValid)
                {
                    return;
                }
                context.Result = BuildBaseSuccessResult(sendIdGuid);
                return;

            case EmailOtp eo:
                // TODO:  We will either send the OTP here or validate it based on if otp exists in the request.
                // SendOtpToEmail(eo.Emails) or ValidateOtp(eo.Emails);
                break;

            default:
                // shouldn’t ever hit this
                throw new InvalidOperationException($"Unknown auth method: {method.GetType()}");
        }
    }

    /// <summary>
    /// tries to parse the send_id from the request.
    /// If it is not present or invalid, sets the result to an error.
    /// </summary>
    /// <param name="context">request context</param>
    /// <returns> the parsed sendId Guid or an empty guid otherwise</returns>
    private static Guid GetRequestSendId(ExtensionGrantValidationContext context)
    {
        var request = context.Request.Raw;
        var sendId = request.Get("send_id");

        if (string.IsNullOrEmpty(sendId))
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidRequest,
                errorDescription: _sendGrantValidatorErrors[SendGrantValidatorResultTypes.MissingSendId]);
            return Guid.Empty;
        }

        var sendIdGuid = new Guid(CoreHelpers.Base64UrlDecode(sendId));

        if (sendIdGuid == Guid.Empty)
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidRequest,
                errorDescription: _sendGrantValidatorErrors[SendGrantValidatorResultTypes.MissingSendId]);
            return Guid.Empty;
        }

        return sendIdGuid;
    }

    private GrantValidationResult BuildBaseSuccessResult(Guid sendId)
    {
        var claims = new List<Claim>
        {
            // TODO: Add email claim when issuing access token for email + OTP send
            new(Claims.SendId, sendId.ToString()),
            new(Claims.Type, IdentityClientType.Send.ToString())
        };

        return new GrantValidationResult(
            subject: sendId.ToString(),
            authenticationMethod: GrantType,
            claims: claims);
    }
}
