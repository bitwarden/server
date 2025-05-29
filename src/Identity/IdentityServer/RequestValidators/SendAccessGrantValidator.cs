using System.Security.Claims;
using Bit.Core.Entities;
using Bit.Core.Identity;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;
using Bit.Core.Utilities;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;

namespace Bit.Identity.IdentityServer.RequestValidators;

public class SendAccessGrantValidator(ISendAuthenticationQuery sendAuthenticationQuery, IPasswordHasher<User> passwordHasher) : IExtensionGrantValidator
{
    public const string GrantType = "send_access";

    string IExtensionGrantValidator.GrantType => GrantType;

    private const string _invalidRequestMissingSendIdMessage = "send_id is required.";
    private const string _invalidRequestPasswordRequiredMessage = "Password is required.";
    private const string _invalidRequestEmailOtpRequiredMessage = "Email and OTP are required.";
    private const string _invalidGrantPasswordInvalid = "Password invalid.";
    // TODO: add email OTP validation error messages here.

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        var request = context.Request.Raw;

        var sendId = request.Get("send_id");

        if (string.IsNullOrEmpty(sendId))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, errorDescription: _invalidRequestMissingSendIdMessage);
            return;
        }

        var sendIdGuid = new Guid(CoreHelpers.Base64UrlDecode(sendId));

        if (sendIdGuid == Guid.Empty)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, errorDescription: _invalidRequestMissingSendIdMessage);
            return;
        }

        // Look up send by id
        var method = await sendAuthenticationQuery.GetAuthenticationMethod(sendIdGuid);

        switch (method)
        {
            case NeverAuthenticate:
                // null send scenario.
                // TODO: Add send enumeration protection here (primarily benefits self hosted instances).
                // We should only map to password or email + OTP protected. If user submits password guess for a
                // falsely protected send, then we will return invalid password.
                // TODO: we should re-use _invalidGrantPasswordInvalid or similar error message here.
                context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "Invalid request");
                return;

            case NotAuthenticated:
                // automatically issue access token
                context.Result = BuildBaseSuccessResult(sendId);
                return;

            case ResourcePassword rp:
                var password = request.Get("password_hash");

                if (string.IsNullOrEmpty(password))
                {
                    context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, errorDescription: _invalidRequestPasswordRequiredMessage);
                    return;
                }

                var passwordValid = ValidateSendPassword(rp.Hash, password);

                if (!passwordValid)
                {
                    context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, errorDescription: _invalidGrantPasswordInvalid);
                    return;
                }

                // password is valid, so we can issue an access token.
                context.Result = BuildBaseSuccessResult(sendId);
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

    private GrantValidationResult BuildBaseSuccessResult(string sendId)
    {
        var claims = new List<Claim>
        {
            // TODO: Add email claim when issuing access token for email + OTP send
            new Claim(Claims.SendId, sendId),
            new Claim(Claims.Type, IdentityClientType.Send.ToString())
        };

        return new GrantValidationResult(
            subject: sendId,
            authenticationMethod: GrantType,
            claims: claims);
    }

    private bool ValidateSendPassword(string sendPassword, string userSubmittedPassword)
    {
        if (string.IsNullOrWhiteSpace(sendPassword) || string.IsNullOrWhiteSpace(userSubmittedPassword))
        {
            return false;
        }
        var passwordResult = passwordHasher.VerifyHashedPassword(new User(), sendPassword, userSubmittedPassword);

        return passwordResult is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
