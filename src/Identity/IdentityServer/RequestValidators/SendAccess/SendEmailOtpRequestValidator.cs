using System.Security.Claims;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Identity;
using Bit.Core.Services;
using Bit.Core.Tools.Models.Data;
using Bit.Identity.IdentityServer.Enums;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess;

public class SendEmailOtpRequestValidator(
    IOtpTokenProvider<DefaultOtpTokenProviderOptions> otpTokenProvider,
    IMailService mailService) : ISendAuthenticationMethodValidator<EmailOtp>
{

    /// <summary>
    /// static object that contains the error messages for the SendPasswordRequestValidator.
    /// </summary>
    private static readonly Dictionary<string, string> _sendPasswordValidatorErrorDescriptions = new()
    {
        { SendAccessConstants.EmailOtpValidatorResults.EmailRequired, $"{SendAccessConstants.TokenRequest.Email} is required." },
        { SendAccessConstants.EmailOtpValidatorResults.EmailOtpSent, "Email otp sent." },
        { SendAccessConstants.EmailOtpValidatorResults.EmailOtpInvalid, $"{SendAccessConstants.TokenRequest.Email} otp is invalid." },
        { SendAccessConstants.EmailOtpValidatorResults.OtpGenerationFailed, $"otp could not be generated" }
    };

    public async Task<GrantValidationResult> ValidateRequestAsync(ExtensionGrantValidationContext context, EmailOtp authMethod, Guid sendId)
    {
        var request = context.Request.Raw;
        // get email
        var email = request.Get(SendAccessConstants.TokenRequest.Email);

        // It is an invalid request if the email is missing which indicated bad shape.
        if (string.IsNullOrEmpty(email))
        {
            // Request is the wrong shape and doesn't contain an email field.
            return BuildErrorResult(SendAccessConstants.EmailOtpValidatorResults.EmailRequired);
        }

        // get otp
        var requestOtp = request.Get(SendAccessConstants.TokenRequest.Otp);
        var uniqueIdentifierForTokenCache = string.Format(SendAccessConstants.OtpToken.TokenUniqueIdentifier, sendId, email);
        if (string.IsNullOrEmpty(requestOtp))
        {
            // Request is the wrong shape and doesn't contain an otp field.
            var token = await otpTokenProvider.GenerateTokenAsync(
                                    SendAccessConstants.OtpToken.TokenProviderName,
                                    SendAccessConstants.OtpToken.Purpose,
                                    uniqueIdentifierForTokenCache);

            if (string.IsNullOrEmpty(token))
            {
                return BuildErrorResult(SendAccessConstants.EmailOtpValidatorResults.OtpGenerationFailed);
            }

            await mailService.SendSendEmailOtpEmailAsync(email, token, SendAccessConstants.OtpToken.Purpose);
            return BuildErrorResult(SendAccessConstants.EmailOtpValidatorResults.EmailOtpSent);
        }

        // validate otp
        var otpResult = await otpTokenProvider.ValidateTokenAsync(
                                requestOtp,
                                SendAccessConstants.OtpToken.TokenProviderName,
                                SendAccessConstants.OtpToken.Purpose,
                                uniqueIdentifierForTokenCache);

        if (!otpResult)
        {
            return BuildErrorResult(SendAccessConstants.EmailOtpValidatorResults.EmailOtpInvalid);
        }

        return BuildSuccessResult(sendId, email!);
    }

    private static GrantValidationResult BuildErrorResult(string error)
    {
        return error switch
        {
            // Request is the wrong shape
            SendAccessConstants.EmailOtpValidatorResults.EmailRequired => new GrantValidationResult(
                                TokenRequestErrors.InvalidRequest,
                                errorDescription: _sendPasswordValidatorErrorDescriptions[SendAccessConstants.EmailOtpValidatorResults.EmailRequired],
                                new Dictionary<string, object>
                                {
                                    { SendAccessConstants.SendAccessError, SendAccessConstants.EmailOtpValidatorResults.EmailRequired }
                                }),
            // Request is correct shape but data is bad
            SendAccessConstants.EmailOtpValidatorResults.EmailOtpInvalid => new GrantValidationResult(
                                TokenRequestErrors.InvalidGrant,
                                errorDescription: _sendPasswordValidatorErrorDescriptions[SendAccessConstants.EmailOtpValidatorResults.EmailOtpInvalid],
                                new Dictionary<string, object>
                                {
                                    { SendAccessConstants.SendAccessError, SendAccessConstants.EmailOtpValidatorResults.EmailOtpInvalid }
                                }),
            // should never get here
            _ => new GrantValidationResult(TokenRequestErrors.InvalidRequest)
        };
    }

    /// <summary>
    /// Builds a successful validation result for the Send password send_access grant.
    /// </summary>
    /// <param name="sendId"></param>
    /// <returns></returns>
    private static GrantValidationResult BuildSuccessResult(Guid sendId, string email)
    {
        var claims = new List<Claim>
        {
            new(Claims.SendAccessClaims.SendId, sendId.ToString()),
            new(Claims.SendAccessClaims.Email, email),
            new(Claims.Type, IdentityClientType.Send.ToString())
        };

        return new GrantValidationResult(
            subject: sendId.ToString(),
            authenticationMethod: CustomGrantTypes.SendAccess,
            claims: claims);
    }
}
