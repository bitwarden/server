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
    /// <param name="model">All necessary information for rotation. If data is not included, this will lead to the change being rejected.</param>
    /// <returns>An IdentityResult for verification of the master password hash</returns>
    /// <exception cref="ArgumentNullException">User must be provided.</exception>
    /// <exception cref="InvalidOperationException">User KDF settings and email must match the model provided settings.</exception>
    Task<IdentityResult> RotateUserAccountKeysAsync(User user, RotateUserAccountKeysData model);
}
