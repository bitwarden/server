using System.Security.Claims;
using Bit.Core.Entities;
using Bit.Core.Identity;
using Bit.Core.Tools.Repositories;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;

namespace Bit.Identity.IdentityServer.RequestValidators;

public class SendAccessGrantValidator(ISendRepository sendRepository, IPasswordHasher<User> passwordHasher) : IExtensionGrantValidator
{
    public const string GrantType = "send_access";

    string IExtensionGrantValidator.GrantType => GrantType;

    private const string _invalidRequestMissingSendIdMessage = "Invalid request. send_id is required.";
    private const string _invalidRequestPasswordRequiredMessage = "Invalid request. Password is required.";
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

        if (!Guid.TryParse(sendId, out var sendIdGuid))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, errorDescription: _invalidRequestMissingSendIdMessage);
            return;
        }

        // TODO: replace repository look up & following logic with use of SendAuthenticationQuery.GetAuthenticationMethod from Tools
        // See below for example of consumption of SendAuthQuery.GetAuthenticationMethod(sendId)

        // Look up send by id
        var send = await sendRepository.GetByIdAsync(sendIdGuid);

        if (send == null)
        {
            // TODO: Add send enumeration protection here (primarily benefits self hosted instances).
            // We should only map to password or email + OTP protected. If user submits password guess for a
            // falsely protected send, then we will return invalid password.
            // TODO: we should re-use _invalidGrantPasswordInvalid or similar error message here.
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "Invalid request");
            return;
        }

        if (!string.IsNullOrEmpty(send.Password))
        {
            // Send is password protected so we need to validate the password.
            var password = request.Get("password");
            if (string.IsNullOrEmpty(password))
            {
                context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, errorDescription: _invalidRequestPasswordRequiredMessage);
                return;
            }

            var passwordValid = ValidateSendPassword(send.Password, password);

            if (!passwordValid)
            {
                context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, errorDescription: _invalidGrantPasswordInvalid);
                return;
            }

            // password is valid, so we can issue an access token.
            context.Result = BuildBaseSuccessResult(sendId);
            return;
        }

        // if send is anon, provide access token

        context.Result = BuildBaseSuccessResult(sendId);


        // Email + OTP - if we generate OTP here, we could run into rate limiting issues with re-hitting this endpoint
        // We will generate & validate OTP here.


        // if send is password protected, check if password is provided and validate if so
        // if send is email + OTP protected, check if email and OTP are provided and validate if so

        // TODO: Example Consumption of SendAuthQuery.GetAuthenticationMethod(sendId) to replace above logic.

        // var method = await sendAuthQuery.GetAuthenticationMethod(sendId);
        //
        // switch (method)
        // {
        //     case NeverAuthenticate:
        //         // null send scenario.
        //         HandleNullSend(); // this is where we add send enumeration protection
        //         break;
        //
        //     case NotAuthenticated:
        //         // automatically issue access token
        //         break;
        //
        //     case ResourcePassword rp:
        //         ValidatePassword(rp.Hash);
        //         break;
        //
        //     case EmailOtp eo:
        //         We will either send the OTP here or validate it.
        //         SendOtpToEmails(eo.Emails) or ValidateOtp(eo.Emails);
        //         break;
        //
        //     default:
        //         // shouldn’t ever hit this
        //         throw new InvalidOperationException($"Unknown auth method: {method.GetType()}");
        // }



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
