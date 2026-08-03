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
    // Key ID of the contained user-key.
    public KeyId? ContainedKeyId { get; init; }

    public void ValidateSaltUnchangedForUser(User user)
    {
        if (user.GetMasterPasswordSalt() != Salt)
        {
            throw new BadRequestException("Invalid master password salt.");
        }
    }

    public void ValidateKeyIdUnchangedForUser(User user)
    {
        var userKeyId = user.GetUserKeyId();
        if (this.ContainedKeyId is null && userKeyId is null)
        {
            return;
        }

        if (this.ContainedKeyId is not null && !this.ContainedKeyId.Equals(userKeyId))
        {
            throw new BadRequestException("Invalid user key id in master password unlock data.");
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
               Salt == other.Salt
                && ContainedKeyId == other.ContainedKeyId;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Kdf, MasterKeyWrappedUserKey, Salt, ContainedKeyId);
    }
}
