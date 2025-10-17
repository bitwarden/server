using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.KeyManagement.Models.Data;

public class MasterPasswordUnlockData
{
    public required KdfSettings Kdf { get; init; }
    public required string MasterKeyWrappedUserKey { get; init; }
    public required string Salt { get; init; }

    public void ValidateSaltUnchangedForUser(User user)
    {
        if (user.GetMasterPasswordSalt() != Salt)
        {
            throw new BadRequestException("Invalid master password salt.");
        }
    }
}
