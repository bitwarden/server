﻿using System.Security.Claims;
using Bit.Core;
using Bit.Core.Identity;
using Bit.Core.Services;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer.Enums;
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
    string IExtensionGrantValidator.GrantType => CustomGrantTypes.SendAccess;

    private static readonly Dictionary<SendGrantValidatorResultTypes, string>
    _sendGrantValidatorErrors = new()
    {
        { SendGrantValidatorResultTypes.MissingSendId, "send_id is required." },
        { SendGrantValidatorResultTypes.InvalidSendId, "send_id is invalid." }
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
        if (result != SendGrantValidatorResultTypes.ValidSendGuid)
        {
            context.Result = BuildErrorResult(result);
            return;
        }

        // Look up send by id
        var method = await _sendAuthenticationQuery.GetAuthenticationMethod(sendIdGuid);

        switch (method)
        {
            case NeverAuthenticate:
                // null send scenario.
                // TODO PM-22675: Add send enumeration protection here (primarily benefits self hosted instances).
                // We should only map to password or email + OTP protected.
                // If user submits password guess for a falsely protected send, then we will return invalid password.
                // If user submits email + OTP guess for a falsely protected send, then we will return email sent, do not actually send an email.
                context.Result = BuildErrorResult(SendGrantValidatorResultTypes.InvalidSendId);
                return;

            case NotAuthenticated:
                // automatically issue access token
                context.Result = BuildBaseSuccessResult(sendIdGuid);
                return;

            case ResourcePassword rp:
                // TODO PM-22675: Validate if the password is correct.
                context.Result = _sendPasswordRequestValidator.ValidateSendPassword(context, rp, sendIdGuid);
                return;
            case EmailOtp eo:
            // TODO PM-22678: We will either send the OTP here or validate it based on if otp exists in the request.
            // SendOtpToEmail(eo.Emails) or ValidateOtp(eo.Emails);
            // break;

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
    private static (Guid, SendGrantValidatorResultTypes) GetRequestSendId(ExtensionGrantValidationContext context)
    {
        var request = context.Request.Raw;
        var sendId = request.Get("send_id");

        // if the sendId is null then the request is the wrong shape and the request is invalid
        if (sendId == null)
        {
            return (Guid.Empty, SendGrantValidatorResultTypes.MissingSendId);
        }
        // the send_id is not null so the request is the correct shape, so we will attempt to parse it
        try
        {
            var guidBytes = CoreHelpers.Base64UrlDecode(sendId);
            var sendGuid = new Guid(guidBytes);
            // Guid.Empty indicates an invalid send_id return invalid grant
            if (sendGuid == Guid.Empty)
            {
                return (Guid.Empty, SendGrantValidatorResultTypes.InvalidSendId);
            }
            return (sendGuid, SendGrantValidatorResultTypes.ValidSendGuid);
        }
        catch
        {
            return (Guid.Empty, SendGrantValidatorResultTypes.InvalidSendId);
        }
    }

    /// <summary>
    /// Builds an error result for the specified error type.
    /// </summary>
    /// <param name="error">The error type.</param>
    /// <returns>The error result.</returns>
    private static GrantValidationResult BuildErrorResult(SendGrantValidatorResultTypes error)
    {
        return error switch
        {
            // Request is the wrong shape
            SendGrantValidatorResultTypes.MissingSendId => new GrantValidationResult(
                                TokenRequestErrors.InvalidRequest,
                                errorDescription: _sendGrantValidatorErrors[SendGrantValidatorResultTypes.MissingSendId]),
            // Request is correct shape but data is bad
            SendGrantValidatorResultTypes.InvalidSendId => new GrantValidationResult(
                                TokenRequestErrors.InvalidGrant,
                                errorDescription: _sendGrantValidatorErrors[SendGrantValidatorResultTypes.InvalidSendId]),
            // should never get here
            _ => new GrantValidationResult(TokenRequestErrors.InvalidRequest)
        };
    }

    private static GrantValidationResult BuildBaseSuccessResult(Guid sendId)
    {
        var claims = new List<Claim>
        {
            new(Claims.SendId, sendId.ToString()),
            new(Claims.Type, IdentityClientType.Send.ToString())
        };

        return new GrantValidationResult(
            subject: sendId.ToString(),
            authenticationMethod: CustomGrantTypes.SendAccess,
            claims: claims);
    }
}
