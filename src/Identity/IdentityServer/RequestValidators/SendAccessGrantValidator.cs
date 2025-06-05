using System.Security.Claims;
using Bit.Core.Identity;
using Bit.Core.KeyManagement.Sends;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.SendFeatures.Queries.Interfaces;
using Bit.Core.Utilities;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators;

public class SendAccessGrantValidator(ISendAuthenticationQuery sendAuthenticationQuery, ISendPasswordHasher sendPasswordHasher) : IExtensionGrantValidator
{
    public const string GrantType = "send_access";

    string IExtensionGrantValidator.GrantType => GrantType;

    private const string _invalidRequestMissingSendIdMessage = "send_id is required.";
    private const string _invalidRequestPasswordRequiredMessage = "Password is required.";
    // TODO: add email + OTP errors here
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
                context.Result = BuildBaseSuccessResult(sendIdGuid);
                return;

            case ResourcePassword rp:
                var password = request.Get("password_hash");

                if (string.IsNullOrEmpty(password))
                {
                    context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, errorDescription: _invalidRequestPasswordRequiredMessage);
                    return;
                }

                var passwordValid = sendPasswordHasher.VerifyPasswordHash(rp.Hash, password);

                if (!passwordValid)
                {
                    context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, errorDescription: _invalidGrantPasswordInvalid);
                    return;
                }

                // password is valid, so we can issue an access token.
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

    private GrantValidationResult BuildBaseSuccessResult(Guid sendId)
    {
        var claims = new List<Claim>
        {
            // TODO: Add email claim when issuing access token for email + OTP send
            new Claim(Claims.SendId, sendId.ToString()),
            new Claim(Claims.Type, IdentityClientType.Send.ToString())
        };

        return new GrantValidationResult(
            subject: sendId.ToString(),
            authenticationMethod: GrantType,
            claims: claims);
    }


}
