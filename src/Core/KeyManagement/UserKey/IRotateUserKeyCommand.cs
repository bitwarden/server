using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace Bit.Core.KeyManagement.UserKey;

/// <summary>
/// Responsible for rotation of a user key and updating database with re-encrypted data
/// </summary>
public interface IRotateUserKeyCommand
{
    /// <summary>
    /// Sets a new user key and updates all encrypted data.
    /// </summary>
    /// <param name="model">All necessary information for rotation. Warning: Any encrypted data not included will be lost.</param>
    /// <returns>An IdentityResult for verification of the master password hash</returns>
    /// <exception cref="ArgumentNullException">User must be provided.</exception>
    Task<IdentityResult> RotateUserKeyAsync(User user, RotateUserKeyData model);
}

/// <summary>
/// A type used to implement updates to the database for key rotations. Each domain that requires an update of encrypted
/// data during a key rotation should use this to implement its own database call. The user repository loops through
/// these during a key rotation.
/// <para>Note: connection and transaction are only used for Dapper. They won't be available in EF</para>
/// </summary>
public delegate Task UpdateEncryptedDataForKeyRotation(
    SqlConnection connection = null,
    SqlTransaction transaction = null
);
