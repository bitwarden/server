using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.KeyManagement.UserKey.Models.Data;

public class MasterPasswordRotateUserAccountKeysData
{
    public required MasterPasswordUnlockData MasterPasswordUnlockData { get; init; }
    public required BaseRotateUserAccountKeysData BaseData { get; init; }

    public void ValidateForUser(User user)
    {
        var isMasterPasswordUser = user is { Key: not null, MasterPassword: not null };
        if (!isMasterPasswordUser)
        {
            throw new BadRequestException("User is in a invalid state for master password key rotation.");
        }

        MasterPasswordUnlockData.ValidateSaltUnchangedForUser(user);
        MasterPasswordUnlockData.Kdf.ValidateUnchangedForUser(user);
    }
}
