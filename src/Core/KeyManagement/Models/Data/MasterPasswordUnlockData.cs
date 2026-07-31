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
    /// Validates that a key id supplied alongside a re-wrap agrees with the one recorded for the
    /// account. Setting or changing a master password produces a new wrapping of the same user key,
    /// so the key id must not change.
    /// <para>
    /// A null on either side is not a disagreement: clients predating the field send no key id, and
    /// legacy accounts have none recorded yet. Backfilling those is the job of
    /// <see cref="Bit.Core.Repositories.IUserRepository.TrySetUserKeyIdAsync"/>, not of a password flow.
    /// </para>
    /// </summary>
    public void ValidateUserKeyIdUnchangedForUser(User user)
    {
        var storedKeyId = user.GetUserKeyId();
        if (storedKeyId == null || UserKeyId == null)
        {
            return;
        }

        if (storedKeyId != UserKeyId)
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
