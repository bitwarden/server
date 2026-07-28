using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Api.Request;

namespace Bit.Core.KeyManagement.Models.Data;

/// <summary>
/// Use this datatype when interfacing with commands, queries, services to create a separation of concern.
/// See <see cref="MasterPasswordUnlockDataRequestModel"/> to use for requests.
/// </summary>
public class MasterPasswordUnlockData
{
    public required KdfSettings Kdf { get; init; }
    public required string MasterKeyWrappedUserKey { get; init; }
    public required string Salt { get; init; }

    /// <summary>
    /// Key-id of the user's user-key, that is wrapped in this master-password unlock data.
    /// </summary>
    public KeyId? UserKeyId { get; init; }

    public KeyId? ContainedKeyId() => UserKeyId;

    public void ValidateSaltUnchangedForUser(User user)
    {
        if (user.GetMasterPasswordSalt() != Salt)
        {
            throw new BadRequestException("Invalid master password salt.");
        }
    }

    /// <summary>
    /// Validates that this unlock data wraps the user key the account already has. Use this in flows
    /// that re-wrap an existing user key rather than replacing it.
    /// <para>
    /// A key rotation deliberately replaces the user key, so rotation flows must not call this.
    /// </para>
    /// </summary>
    public void ValidateUserKeyUnchangedForUser(User user)
    {
        // Nothing to compare against when the server does not have a key id for the account yet, or
        // when the client did not report one.
        if (user.GetUserKeyId() == null || this.UserKeyId == null)
        {
            return;
        }

        if (user.GetUserKeyId() != this.UserKeyId)
        {
            throw new BadRequestException("Invalid user key id.");
        }
    }

    public override bool Equals(object? obj)
    {
        if (obj is not MasterPasswordUnlockData other)
        {
            return false;
        }

        return Kdf.Equals(other.Kdf) &&
               MasterKeyWrappedUserKey == other.MasterKeyWrappedUserKey &&
               Salt == other.Salt &&
               KeyId.Equals(UserKeyId, other.UserKeyId);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Kdf, MasterKeyWrappedUserKey, Salt, UserKeyId);
    }
}
