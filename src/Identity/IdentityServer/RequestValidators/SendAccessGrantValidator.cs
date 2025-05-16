using System.Security.Claims;
using Bit.Core.Identity;
using Bit.Core.Tools.Repositories;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators;

// TODO: in real implementation, we would use ideally use Tools provided Send queries for the data we need
// instead of directly injecting the send repository here.
public class SendAccessGrantValidator(ISendRepository sendRepository) : IExtensionGrantValidator
{
    public const string GrantType = "send_access";

    string IExtensionGrantValidator.GrantType => GrantType;

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        var request = context.Request.Raw;

        var sendId = request.Get("send_id");

        if (string.IsNullOrEmpty(sendId))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "Invalid request. send_id is required.");
            return;
        }

        if (!Guid.TryParse(sendId, out var sendIdGuid))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "Invalid request. send_id is required.");
            return;
        }

        // Look up send by id
        var send = await sendRepository.GetByIdAsync(sendIdGuid);

        // SendAuthQuery from Tools will return authN type + if a send doesn't exist, then we will add enumeration protection
        // Only will map to password or email + OTP protected. If user submits password guess for a falsely protected send, then
        // return invalid password.

        if (send == null)
        {
            // TODO: evaluate if adding send enumeration protection is required here as rate limiting is present already on the token endpoint
            // Yes, it does help with self hosted instances. We will
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "Invalid request");
            return;
        }


        // if send is anon, provide access token
        if (string.IsNullOrEmpty(send.Password))
        {
            // context.Result = new GrantValidationResult(subject: sendId);
            context.Result = BuildBaseSuccessResult(sendId);
            return;
        }


        // Email + OTP - if we generate OTP here, we could run into rate limiting issues with re-hitting this endpoint
        // We will generate & validate OTP here.


        // if send is password protected, check if password is provided and validate if so
        // if send is email + OTP protected, check if email and OTP are provided and validate if so


        context.Result = new GrantValidationResult("send_access", GrantType);
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
}
