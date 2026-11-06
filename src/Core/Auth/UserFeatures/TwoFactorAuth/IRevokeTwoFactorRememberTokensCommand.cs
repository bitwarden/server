namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth;

public interface IRevokeTwoFactorRememberTokensCommand
{
    /// <summary>
    /// Stops every 2FA remember token recorded for this user from being honored, on all of their devices.
    /// </summary>
    Task RevokeAllForUserAsync(Guid userId);
}
