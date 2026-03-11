using Bit.Core.Tools.Models.Data;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators.SendAccess;

/// <summary>
/// Validator for sends that exist but cannot be accessed (expired, disabled, max access exceeded, or past deletion date).
/// Always returns <see cref="SendAccessConstants.SendIdGuidValidatorResults.InvalidSendId"/> without randomization,
/// since the send's ID was already publicly shared and enumeration protection is not needed here.
/// </summary>
public class SendInaccessibleRequestValidator : ISendAuthenticationMethodValidator<SendInaccessible>
{
    public Task<GrantValidationResult> ValidateRequestAsync(
        ExtensionGrantValidationContext context,
        SendInaccessible authMethod,
        Guid sendId)
    {
        var customResponse = new Dictionary<string, object>
        {
            { SendAccessConstants.SendAccessError, SendAccessConstants.SendIdGuidValidatorResults.InvalidSendId }
        };

        return Task.FromResult(new GrantValidationResult(
            TokenRequestErrors.InvalidGrant,
            SendAccessConstants.SendIdGuidValidatorResults.InvalidSendId,
            customResponse));
    }
}
