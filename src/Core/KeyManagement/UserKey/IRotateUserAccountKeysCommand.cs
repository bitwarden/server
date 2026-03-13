// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.UserKey.Models.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace Bit.Core.KeyManagement.UserKey;

/// <summary>
/// Responsible for rotation of a user key and updating database with re-encrypted data
/// </summary>
public interface IRotateUserAccountKeysCommand
{
    /// <summary>
    /// Sets a new user key and updates all encrypted data and data associated with a password change.
    /// </summary>
    /// <param name="model">All necessary information for rotation and password change. If data is not included, this will lead to the change being rejected.</param>
    /// <returns>An IdentityResult for verification of the master password hash</returns>
    /// <exception cref="ArgumentNullException">User must be provided.</exception>
    /// <exception cref="InvalidOperationException">User KDF settings and email must match the model provided settings.</exception>
    Task<IdentityResult> PasswordChangeAndRotateUserAccountKeysAsync(User user, PasswordChangeAndRotateUserAccountKeysData model);

    /// <summary>
    /// For a master password user, rotates the user key and updates all encrypted data without changing the master password.
    /// </summary>
    /// <param name="model">Rotation data. All encrypted data must be included or the request will be rejected.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is null.</exception>
    /// <exception cref="BadRequestException">Thrown when  <paramref name="user"/> is not a master password user.</exception>
    /// <exception cref="BadRequestException">Thrown when <paramref name="user"/> salt does not match <paramref name="model"/> MasterPasswordUnlockData.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="user"/> KDF settings do not match <paramref name="model"/> MasterPasswordUnlockData.</exception>
    Task MasterPasswordRotateUserAccountKeysAsync(User user, MasterPasswordRotateUserAccountKeysData model);
}

/// <summary>
/// A type used to implement updates to the database for key rotations. Each domain that requires an update of encrypted
/// data during a key rotation should use this to implement its own database call. The user repository loops through
/// these during a key rotation.
/// <para>Note: connection and transaction are only used for Dapper. They won't be available in EF</para>
/// </summary>
public delegate Task UpdateEncryptedDataForKeyRotation(SqlConnection connection = null,
    SqlTransaction transaction = null);
