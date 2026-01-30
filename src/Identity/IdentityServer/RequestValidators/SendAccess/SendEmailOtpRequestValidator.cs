using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bit.Core;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Services;
using Bit.Core.Tools.Models.Data;
using Bit.Identity.IdentityServer.Enums;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess;

public class SendEmailOtpRequestValidator(
    IFeatureService featureService,
    IOtpTokenProvider<DefaultOtpTokenProviderOptions> otpTokenProvider,
    IMailService mailService) : ISendAuthenticationMethodValidator<EmailOtp>
{

    /// <summary>
    /// static object that contains the error messages for the SendEmailOtpRequestValidator.
    /// </summary>
    private static readonly Dictionary<string, string> _sendEmailOtpValidatorErrorDescriptions = new()
    {
        { SendAccessConstants.EmailOtpValidatorResults.EmailRequired, $"{SendAccessConstants.TokenRequest.Email} is required." },
        { SendAccessConstants.EmailOtpValidatorResults.EmailAndOtpRequired, $"{SendAccessConstants.TokenRequest.Email} and {SendAccessConstants.TokenRequest.Otp} are required." },
        { SendAccessConstants.EmailOtpValidatorResults.EmailOtpInvalid, $"{SendAccessConstants.TokenRequest.Email} otp is invalid." },
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

        // email hash must be in the list of email hashes in the EmailOtp array
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(email));
        string hashEmailHex = Convert.ToHexString(hashBytes).ToUpperInvariant();
        /*
         * This is somewhat contradictory to our process where a poor shape means invalid_request and invalid
         * data is invalid_grant.
         * In this case the shape is correct and the data is invalid but to protect against enumeration we treat incorrect emails
         * as invalid requests. The response for a request with a correct email which needs an OTP and a request
         * that has an invalid email need to be the same otherwise an attacker could enumerate until a valid email is found.
        */
        if (!authMethod.EmailHashes.Contains(hashEmailHex))
        {
            return BuildErrorResult(SendAccessConstants.EmailOtpValidatorResults.EmailAndOtpRequired);
        }

        // get otp from request
        var requestOtp = request.Get(SendAccessConstants.TokenRequest.Otp);
        var uniqueIdentifierForTokenCache = string.Format(SendAccessConstants.OtpToken.TokenUniqueIdentifier, sendId, email);
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
                return BuildErrorResult(SendAccessConstants.EmailOtpValidatorResults.OtpGenerationFailed);
            }
            if (featureService.IsEnabled(FeatureFlagKeys.MJMLBasedEmailTemplates))
            {
                await mailService.SendSendEmailOtpEmailv2Async(
                    email,
                    token,
                    string.Format(SendAccessConstants.OtpEmail.Subject, token));
            }
            else
            {
                await mailService.SendSendEmailOtpEmailAsync(
                    email,
                    token,
                    string.Format(SendAccessConstants.OtpEmail.Subject, token));
            }
            return BuildErrorResult(SendAccessConstants.EmailOtpValidatorResults.EmailAndOtpRequired);
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
            return BuildErrorResult(SendAccessConstants.EmailOtpValidatorResults.EmailOtpInvalid);
        }

        return BuildSuccessResult(sendId, email!);
    }

    private static GrantValidationResult BuildErrorResult(string error)
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
            case SendAccessConstants.EmailOtpValidatorResults.EmailOtpInvalid:
                return new GrantValidationResult(
                    TokenRequestErrors.InvalidGrant,
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
