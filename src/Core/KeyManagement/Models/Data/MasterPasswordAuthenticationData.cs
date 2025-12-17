using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Api.Request;

namespace Bit.Core.KeyManagement.Models.Data;

/// <summary>
/// Use this datatype when interfacing with commands, queries, services to create a separation of concern.
/// See <see cref="MasterPasswordAuthenticationDataRequestModel"/> to use for requests.
/// </summary>
public class MasterPasswordAuthenticationData
{
    public required KdfSettings Kdf { get; init; }
    public required string MasterPasswordAuthenticationHash { get; init; }
    public required string Salt { get; init; }

    public void ValidateSaltUnchangedForUser(User user)
    {
        if (user.GetMasterPasswordSalt() != Salt)
        {
            throw new BadRequestException("Invalid master password salt.");
        }
    }

    public override bool Equals(object? obj)
    {
        if (obj is not MasterPasswordAuthenticationData other)
        {
            return false;
        }

        return Kdf.Equals(other.Kdf) &&
               MasterPasswordAuthenticationHash == other.MasterPasswordAuthenticationHash &&
               Salt == other.Salt;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Kdf, MasterPasswordAuthenticationHash, Salt);
    }
}
