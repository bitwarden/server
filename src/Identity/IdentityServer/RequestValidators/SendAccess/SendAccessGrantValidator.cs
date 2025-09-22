using System.Security.Claims;
using Bit.Core;
using Bit.Core.Auth.Identity;
using Bit.Core.Services;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer.Enums;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess;

public class SendAccessGrantValidator(
    ISendAuthenticationQuery _sendAuthenticationQuery,
    ISendAuthenticationMethodValidator<NeverAuthenticate> _sendNeverAuthenticateValidator,
    ISendAuthenticationMethodValidator<ResourcePassword> _sendPasswordRequestValidator,
    ISendAuthenticationMethodValidator<EmailOtp> _sendEmailOtpRequestValidator,
    IFeatureService _featureService) : IExtensionGrantValidator
{
    string IExtensionGrantValidator.GrantType => CustomGrantTypes.SendAccess;

    private static readonly Dictionary<string, string> _sendGrantValidatorErrorDescriptions = new()
    {
        { SendAccessConstants.SendIdGuidValidationResults.SendIdRequired, $"{SendAccessConstants.TokenRequest.SendId} is required." },
        { SendAccessConstants.SendIdGuidValidationResults.InvalidSendId, $"{SendAccessConstants.TokenRequest.SendId} is invalid." }
    };

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        // Check the feature flag
        if (!_featureService.IsEnabled(FeatureFlagKeys.SendAccess))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.UnsupportedGrantType);
            return;
        }

        var (sendIdGuid, result) = GetRequestSendId(context);
        if (result != SendAccessConstants.SendIdGuidValidationResults.ValidSendGuid)
        {
            context.Result = BuildErrorResult(result);
            return;
        }

        // Look up send by id
        var method = await _sendAuthenticationQuery.GetAuthenticationMethod(sendIdGuid);

        switch (method)
        {
            case NeverAuthenticate never:
                // null send scenario.
                context.Result = await _sendNeverAuthenticateValidator.ValidateRequestAsync(context, never, sendIdGuid);
                return;
            case NotAuthenticated:
                // automatically issue access token
                context.Result = BuildBaseSuccessResult(sendIdGuid);
                return;
            case ResourcePassword rp:
                // Validate if the password is correct, or if we need to respond with a 400 stating a password is invalid or required.
                context.Result = await _sendPasswordRequestValidator.ValidateRequestAsync(context, rp, sendIdGuid);
                return;
            case EmailOtp eo:
                // Validate if the request has the correct email and OTP. If not, respond with a 400 and information about the failure.
                context.Result = await _sendEmailOtpRequestValidator.ValidateRequestAsync(context, eo, sendIdGuid);
                return;
            default:
                // shouldn’t ever hit this
                throw new InvalidOperationException($"Unknown auth method: {method.GetType()}");
        }
    }

    /// <summary>
    /// tries to parse the send_id from the request.
    /// If it is not present or invalid, sets the correct result error.
    /// </summary>
    /// <param name="context">request context</param>
    /// <returns>a parsed sendId Guid and success result or a Guid.Empty and error type otherwise</returns>
    private static (Guid, string) GetRequestSendId(ExtensionGrantValidationContext context)
    {
        var request = context.Request.Raw;
        var sendId = request.Get(SendAccessConstants.TokenRequest.SendId);

        // if the sendId is null then the request is the wrong shape and the request is invalid
        if (sendId == null)
        {
            return (Guid.Empty, SendAccessConstants.SendIdGuidValidationResults.SendIdRequired);
        }
        // the send_id is not null so the request is the correct shape, so we will attempt to parse it
        try
        {
            var guidBytes = CoreHelpers.Base64UrlDecode(sendId);
            var sendGuid = new Guid(guidBytes);
            // Guid.Empty indicates an invalid send_id return invalid grant
            if (sendGuid == Guid.Empty)
            {
                return (Guid.Empty, SendAccessConstants.SendIdGuidValidationResults.InvalidSendId);
            }
            return (sendGuid, SendAccessConstants.SendIdGuidValidationResults.ValidSendGuid);
        }
        catch
        {
            return (Guid.Empty, SendAccessConstants.SendIdGuidValidationResults.InvalidSendId);
        }
    }

    /// <summary>
    /// Builds an error result for the specified error type.
    /// </summary>
    /// <param name="error">This error is a constant string from <see cref="SendAccessConstants.SendIdGuidValidationResults"/></param>
    /// <returns>The error result.</returns>
    private static GrantValidationResult BuildErrorResult(string error)
    {
        var customResponse = new Dictionary<string, object>
            {
                { SendAccessConstants.SendAccessError, error }
            };

        return error switch
        {
            // Request is the wrong shape
            SendAccessConstants.SendIdGuidValidationResults.SendIdRequired => new GrantValidationResult(
                                TokenRequestErrors.InvalidRequest,
                                errorDescription: _sendGrantValidatorErrorDescriptions[error],
                                customResponse),
            // Request is correct shape but data is bad
            SendAccessConstants.SendIdGuidValidationResults.InvalidSendId => new GrantValidationResult(
                                TokenRequestErrors.InvalidGrant,
                                errorDescription: _sendGrantValidatorErrorDescriptions[error],
                                customResponse),
            // should never get here
            _ => new GrantValidationResult(TokenRequestErrors.InvalidRequest)
        };
    }

    private static GrantValidationResult BuildBaseSuccessResult(Guid sendId)
    {
        var claims = new List<Claim>
        {
            new(Claims.SendAccessClaims.SendId, sendId.ToString()),
            new(Claims.Type, IdentityClientType.Send.ToString())
        };

        return new GrantValidationResult(
            subject: sendId.ToString(),
            authenticationMethod: CustomGrantTypes.SendAccess,
            claims: claims);
    }
}
