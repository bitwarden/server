using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.KeyManagement.UserKey;

/// <summary>
/// Responsible for rotation of a user key and updating database with re-encrypted data
/// </summary>
public interface IRotateUserAccountKeysCommand
{
    /// <summary>
    /// Sets a new user key and updates all encrypted data.
    /// </summary>
    /// <param name="model">All necessary information for rotation. Warning: Any encrypted data not included will be lost.</param>
    /// <returns>An IdentityResult for verification of the master password hash</returns>
    /// <exception cref="ArgumentNullException">User must be provided.</exception>
    Task<IdentityResult> rotateUserAccountKeysAsync(User user, RotateUserAccountKeysData model);
}
