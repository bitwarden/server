using System.Security.Claims;
using Bit.Core.Identity;
using Bit.Core.KeyManagement.Sends;
using Bit.Core.Tools.Models.Data;
using Bit.Identity.IdentityServer.Enums;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess;

public class SendPasswordRequestValidator(ISendPasswordHasher sendPasswordHasher) : ISendPasswordRequestValidator
{
    private readonly ISendPasswordHasher _sendPasswordHasher = sendPasswordHasher;

    /// <summary>
    /// static object that contains the error messages for the SendPasswordRequestValidator.
    /// </summary>
    private static readonly Dictionary<string, string> _sendPasswordValidatorErrorDescriptions = new()
    {
        { SendAccessConstants.PasswordValidatorResults.RequestPasswordDoesNotMatch, $"{SendAccessConstants.TokenRequest.ClientB64HashedPassword} is invalid." },
        { SendAccessConstants.PasswordValidatorResults.RequestPasswordIsRequired, $"{SendAccessConstants.TokenRequest.ClientB64HashedPassword} is required." }
    };

    public GrantValidationResult ValidateSendPassword(ExtensionGrantValidationContext context, ResourcePassword resourcePassword, Guid sendId)
    {
        var request = context.Request.Raw;
        var clientHashedPassword = request.Get(SendAccessConstants.TokenRequest.ClientB64HashedPassword);

        // It is an invalid request _only_ if the passwordHashB64 is missing which indicated bad shape.
        if (clientHashedPassword == null)
        {
            // Request is the wrong shape and doesn't contain a passwordHashB64 field.
            return new GrantValidationResult(
                TokenRequestErrors.InvalidRequest,
                errorDescription: _sendPasswordValidatorErrorDescriptions[SendAccessConstants.PasswordValidatorResults.RequestPasswordIsRequired],
                new Dictionary<string, object>
                {
                    { SendAccessConstants.SendAccessError, SendAccessConstants.PasswordValidatorResults.RequestPasswordIsRequired }
                });
        }

        // _sendPasswordHasher.PasswordHashMatches checks for an empty string so no need to do it before we make the call.
        var hashMatches = _sendPasswordHasher.PasswordHashMatches(
            resourcePassword.Hash, clientHashedPassword);

        if (!hashMatches)
        {
            // Request is the correct shape but the passwordHashB64 doesn't match, hash could be empty.
            return new GrantValidationResult(
                TokenRequestErrors.InvalidGrant,
                errorDescription: _sendPasswordValidatorErrorDescriptions[SendAccessConstants.PasswordValidatorResults.RequestPasswordDoesNotMatch],
                new Dictionary<string, object>
                {
                    { SendAccessConstants.SendAccessError, SendAccessConstants.PasswordValidatorResults.RequestPasswordDoesNotMatch }
                });
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
