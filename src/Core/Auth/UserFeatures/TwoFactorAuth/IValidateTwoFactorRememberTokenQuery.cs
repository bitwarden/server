using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth;

public interface IValidateTwoFactorRememberTokenQuery
{
    /// <summary>
    /// Decides whether a presented remember token entitles this request to skip the two-factor
    /// challenge.
    /// </summary>
    /// <remarks>
    /// A pure read: nothing here writes. If a last-used timestamp is ever wanted, it belongs in its
    /// own command rather than folded in here.
    /// </remarks>
    /// <param name="user">The authenticating user.</param>
    /// <param name="deviceIdentifier">
    /// The client-generated device identifier from the current request. The device row itself is not
    /// available at this point in the login pipeline, which is why the identifier is passed in.
    /// </param>
    /// <param name="token">The protected token string as presented by the client.</param>
    Task<bool> ValidateAsync(User user, string deviceIdentifier, string token);
}
