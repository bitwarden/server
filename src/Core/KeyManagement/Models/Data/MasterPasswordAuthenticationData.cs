using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.KeyManagement.Models.Data;

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

    public static void ThrowIfExistsAndHashIsNotEqual(
        MasterPasswordAuthenticationData? authenticationData,
        string? hash)
    {
        if (authenticationData != null && hash != null)
        {
            if (authenticationData.MasterPasswordAuthenticationHash != hash)
            {
                throw new Exception("Master password hash and hash are not equal.");
            }
        }
    }
}
