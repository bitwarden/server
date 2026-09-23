namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth;

public interface IRevokeTwoFactorRememberTokensCommand
{
    /// <summary>
    /// Stops every remember token this user holds from being honored, on all of their devices.
    /// </summary>
    /// <remarks>
    /// Scoped to remember-me only: access tokens, refresh tokens, and live sessions are unaffected,
    /// so this does not log the user out anywhere. Devices stay remembered as rows — they simply
    /// require a two-factor challenge again on next login.
    /// </remarks>
    Task RevokeAllForUserAsync(Guid userId);
}
