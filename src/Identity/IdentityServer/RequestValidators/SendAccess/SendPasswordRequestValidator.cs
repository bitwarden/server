using System.Security.Claims;
using Bit.Core.Identity;
using Bit.Core.KeyManagement.Sends;
using Bit.Core.Tools.Models.Data;
using Bit.Identity.IdentityServer.Enums;
using Bit.Identity.IdentityServer.RequestValidators.SendAccess.Enums;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess;

public class SendPasswordRequestValidator(ISendPasswordHasher sendPasswordHasher) : ISendPasswordRequestValidator
{
    private readonly ISendPasswordHasher _sendPasswordHasher = sendPasswordHasher;

    /// <summary>
    /// static object that contains the error messages for the SendPasswordRequestValidator.
    /// </summary>
    private static Dictionary<SendPasswordValidatorResultTypes, string> _sendPasswordValidatorErrors = new()
    {
        { SendPasswordValidatorResultTypes.RequestPasswordDoesNotMatch, "Request Password hash is invalid." }
    };

    public GrantValidationResult ValidateSendPassword(ExtensionGrantValidationContext context, ResourcePassword resourcePassword, Guid sendId)
    {
        var request = context.Request.Raw;
        var clientHashedPassword = request.Get("password_hash");

        if (string.IsNullOrEmpty(clientHashedPassword))
        {
            return new GrantValidationResult(
                TokenRequestErrors.InvalidRequest,
                errorDescription: _sendPasswordValidatorErrors[SendPasswordValidatorResultTypes.RequestPasswordDoesNotMatch]);
        }

        var hashMatches = _sendPasswordHasher.PasswordHashMatches(
            resourcePassword.Hash, clientHashedPassword);

        if (!hashMatches)
        {
            return new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                errorDescription: _sendPasswordValidatorErrors[SendPasswordValidatorResultTypes.RequestPasswordDoesNotMatch]);
        }

        return BuildSendPasswordSuccessResult(sendId);
    }

    /// <summary>
    /// Builds a successful validation result for the Send password send_access grant.
    /// </summary>
    /// <param name="sendId"></param>
    /// <returns></returns>
    private static GrantValidationResult BuildSendPasswordSuccessResult(Guid sendId)
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
