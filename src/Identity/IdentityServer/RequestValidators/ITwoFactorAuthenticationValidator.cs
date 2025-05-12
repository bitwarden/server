
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer.RequestValidators;

public interface ITwoFactorAuthenticationValidator
{
    /// <summary>
    /// Check if the user is required to use two-factor authentication to login. This is based on the user's
    /// enabled two-factor providers, the user's organizations enabled two-factor providers, and the grant type.
    /// Client credentials and webauthn grant types do not require two-factor authentication.
    /// </summary>
    /// <param name="user">the active user for the request</param>
    /// <param name="request">the request that contains the grant types</param>
    /// <returns>boolean</returns>
    Task<Tuple<bool, Organization>> RequiresTwoFactorAsync(User user, ValidatedTokenRequest request);
    /// <summary>
    /// Builds the two-factor authentication result for the user based on the available two-factor providers
    /// from either their user account or Organization.
    /// </summary>
    /// <param name="user">user trying to login</param>
    /// <param name="organization">organization associated with the user; Can be null</param>
    /// <returns>Dictionary with the TwoFactorProviderType as the Key and the Provider Metadata as the Value</returns>
    Task<Dictionary<string, object>> BuildTwoFactorResultAsync(User user, Organization organization);
    /// <summary>
    /// Uses the built in userManager methods to verify the two-factor token for the user. If the organization uses
    /// organization duo, it will use the organization duo token provider to verify the token.
    /// </summary>
    /// <param name="user">the active User</param>
    /// <param name="organization">organization of user; can be null</param>
    /// <param name="twoFactorProviderType">Two Factor Provider to use to verify the token</param>
    /// <param name="token">secret passed from the user and consumed by the two-factor provider's verify method</param>
    /// <returns>boolean</returns>
    Task<bool> VerifyTwoFactorAsync(User user, Organization organization, TwoFactorProviderType twoFactorProviderType, string token);
}
