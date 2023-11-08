using Bit.Core.Auth.Models.Data;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserKey;

public interface IRotateUserKeyCommand
{
    /// <summary>
    /// Sets a new user key and updates all encrypted data.
    /// </summary>
    /// <param name="model">All necessary information for rotation. Warning: Any encrypted data not included will be lost.</param>
    /// <returns>An IdentityResult for verification of the master password hash</returns>
    /// <exception cref="ArgumentNullException">User must be provided.</exception>
    Task<IdentityResult> RotateUserKeyAsync(RotateUserKeyData model);
}
