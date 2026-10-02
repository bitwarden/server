using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth;

public interface IIssueTwoFactorRememberTokenCommand
{
    /// <summary>
    /// Records that this device is remembered and mints the token the client presents to skip the
    /// two-factor challenge on its next login.
    /// </summary>
    /// <remarks>
    /// Writing the row assigns it a fresh stamp, so any token previously issued for this device
    /// stops being honored — one live remember token per device, newest wins.
    /// </remarks>
    /// <returns>The protected token string, to be returned to the client verbatim.</returns>
    Task<string> IssueAsync(User user, Device device);
}
