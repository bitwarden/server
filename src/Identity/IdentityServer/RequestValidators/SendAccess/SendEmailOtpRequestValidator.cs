using System.Globalization;
using System.Security.Claims;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Services;
using Bit.Core.Tools.Models.Data;
using Bit.Identity.IdentityServer.Enums;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess;

/**
* The error responses here do not fully match the standard for OAuth with respect to Invalid Request vs Invalid Grant. This is intended to better protect
* against enumeration. We return Invalid Request for all errors related to the email and OTP, even if in some cases Invalid Grant might be more appropriate.
*/
public class SendEmailOtpRequestValidator(
    ILogger<SendEmailOtpRequestValidator> logger,
    IOtpTokenProvider<DefaultOtpTokenProviderOptions> otpTokenProvider,
    IMailService mailService) : ISendAuthenticationMethodValidator<EmailOtp>
{

    /// <summary>
    /// static object that contains the error messages for the SendEmailOtpRequestValidator.
    /// </summary>
    private static readonly Dictionary<string, string> _sendEmailOtpValidatorErrorDescriptions = new()
    {
        { SendAccessConstants.EmailOtpValidatorResults.EmailRequired, $"{SendAccessConstants.TokenRequest.Email} is required." },
        { SendAccessConstants.EmailOtpValidatorResults.EmailAndOtpRequired, $"{SendAccessConstants.TokenRequest.Email} and {SendAccessConstants.TokenRequest.Otp} are required." }
    };

    public async Task<GrantValidationResult> ValidateRequestAsync(ExtensionGrantValidationContext context, EmailOtp authMethod, Guid sendId)
    {
        var request = context.Request.Raw;
        // get email
        var email = request.Get(SendAccessConstants.TokenRequest.Email);

        // It is an invalid request if the email is missing.
        if (string.IsNullOrEmpty(email))
        {
            // Request is the wrong shape and doesn't contain an email field.
            return BuildErrorResult(SendAccessConstants.EmailOtpValidatorResults.EmailRequired);
        }

        if (!authMethod.emails.Contains(email, StringComparer.OrdinalIgnoreCase))
        {
            return BuildErrorResult();
        }

        // get otp from request
        var requestOtp = request.Get(SendAccessConstants.TokenRequest.Otp);
        var uniqueIdentifierForTokenCache = string.Format(CultureInfo.InvariantCulture, SendAccessConstants.OtpToken.TokenUniqueIdentifier, sendId, email);
        if (string.IsNullOrEmpty(requestOtp))
        {
            // Since the request doesn't have an OTP, generate one
            var token = await otpTokenProvider.GenerateTokenAsync(
                                    SendAccessConstants.OtpToken.TokenProviderName,
                                    SendAccessConstants.OtpToken.Purpose,
                                    uniqueIdentifierForTokenCache);

            // Verify that the OTP is generated
            if (string.IsNullOrEmpty(token))
            {
                logger.LogWarning("Failed to generate OTP for SendAccess");
                return BuildErrorResult();
            }

            await mailService.SendSendEmailOtpEmailAsync(
                email,
                token,
                string.Format(CultureInfo.CurrentCulture, SendAccessConstants.OtpEmail.Subject, token));

            return BuildErrorResult();
        }

        // validate request otp
        var otpResult = await otpTokenProvider.ValidateTokenAsync(
                                requestOtp,
                                SendAccessConstants.OtpToken.TokenProviderName,
                                SendAccessConstants.OtpToken.Purpose,
                                uniqueIdentifierForTokenCache);

        // If OTP is invalid return error result
        if (!otpResult)
        {
            return BuildErrorResult();
        }

        return BuildSuccessResult(sendId, email!);
    }

    /// <summary>
    /// Build the error response for the SendEmailOtpRequestValidator.
    /// </summary>
    /// <param name="error">The error code to use for the validation result. This is defaulted to EmailAndOtpRequired if not specified because it is the most common response.</param>
    /// <returns>A GrantValidationResult representing the error.</returns>
    private static GrantValidationResult BuildErrorResult(string error = SendAccessConstants.EmailOtpValidatorResults.EmailAndOtpRequired)
    {
        switch (error)
        {
            case SendAccessConstants.EmailOtpValidatorResults.EmailRequired:
            case SendAccessConstants.EmailOtpValidatorResults.EmailAndOtpRequired:
                return new GrantValidationResult(TokenRequestErrors.InvalidRequest,
                    errorDescription: _sendEmailOtpValidatorErrorDescriptions[error],
                    new Dictionary<string, object>
                    {
                        { SendAccessConstants.SendAccessError, error }
                    });
            default:
                return new GrantValidationResult(
                    TokenRequestErrors.InvalidRequest,
                    errorDescription: error);
        }
    }

    /// <summary>
    /// Builds a successful validation result for the Send password send_access grant.
    /// </summary>
    /// <param name="sendId">Guid of the send being accessed.</param>
    /// <returns>successful grant validation result</returns>
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
