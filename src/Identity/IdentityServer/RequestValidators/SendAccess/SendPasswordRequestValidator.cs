using Bit.Core.KeyManagement.Sends;
using Bit.Core.Tools.Models.Data;
using Bit.Identity.IdentityServer.RequestValidators.SendAccess.Enums;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess;

public class SendPasswordRequestValidator(ISendPasswordHasher sendPasswordHasher) : ISendPasswordRequestValidator
{
    private readonly ISendPasswordHasher _sendPasswordHasher = sendPasswordHasher;

    public bool ValidateSendPassword(ExtensionGrantValidationContext context, ResourcePassword resourcePassword)
    {
        var request = context.Request.Raw;
        var clientHashedPassword = request.Get("password_hash");

        if (string.IsNullOrEmpty(clientHashedPassword))
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidRequest,
                errorDescription: _sendPasswordValidatorErrors[SendPasswordValidatorResultTypes.RequestPasswordNullOrEmpty]);
            return false;
        }

        var hashMatches = _sendPasswordHasher.PasswordHashMatches(
            resourcePassword.Hash, clientHashedPassword);

        if (!hashMatches)
        {
            context.Result = new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                errorDescription: _sendPasswordValidatorErrors[SendPasswordValidatorResultTypes.RequestPasswordDoesNotMatch]);
            return false;
        }
        else
        {
            return true;
        }
    }

    private static Dictionary<SendPasswordValidatorResultTypes, string> _sendPasswordValidatorErrors = new()
    {
        { SendPasswordValidatorResultTypes.SendPasswordNullOrEmpty, "Send password is null or empty." },
        { SendPasswordValidatorResultTypes.RequestPasswordNullOrEmpty, "Request Password hash is required." },
        { SendPasswordValidatorResultTypes.RequestPasswordDoesNotMatch, "Request Password hash is invalid." }
    };
}
