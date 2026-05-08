using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth;

public interface IResetUserTwoFactorCommand
{
    /// <summary>
    /// Resets all two-factor authentication methods for a user.
    /// Clears all configured providers, sets the recovery code to null,
    /// and bumps the user's revision dates.
    /// </summary>
    /// <param name="user">The user whose 2FA methods should be reset.</param>
    Task ResetAsync(User user);
}
