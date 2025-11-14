using Bit.Core.Entities;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators;

/// <summary>
/// Validates whether a user is required to authenticate via SSO based on organization policies.
/// </summary>
public interface ISsoRequestValidator
{
    /// <summary>
    /// Validates the SSO requirement for a user attempting to authenticate. Sets the error state in the <see cref="CustomValidatorRequestContext.CustomResponse"/> if SSO is required.
    /// </summary>
    /// <param name="user">The user attempting to authenticate.</param>
    /// <param name="request">The token request containing grant type and other authentication details.</param>
    /// <param name="context">The validator context to be updated with SSO requirement status and error results if applicable.</param>
    /// <returns>true if the user can proceed with authentication; false if SSO is required and the user must be redirected to SSO flow.</returns>
    Task<bool> ValidateAsync(User user, ValidatedTokenRequest request, CustomValidatorRequestContext context);
}
