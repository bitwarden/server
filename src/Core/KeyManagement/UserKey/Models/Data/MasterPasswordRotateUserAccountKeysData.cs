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
            throw new BadRequestException("User is in an invalid state for master password key rotation.");
        }

        MasterPasswordUnlockData.ValidateSaltUnchangedForUser(user);
        MasterPasswordUnlockData.Kdf.ValidateUnchangedForUser(user);
        ValidateKeyIdMatches();
    }

    private void ValidateKeyIdMatches()
    {
        var both_null = MasterPasswordUnlockData.ContainedKeyId == null && BaseData.NewUserKeyId == null;
        var both_not_null = MasterPasswordUnlockData.ContainedKeyId != null && BaseData.NewUserKeyId != null;
        
        if (both_null)
        {
            return;
        } else if (both_not_null)
        {
            if (!MasterPasswordUnlockData.ContainedKeyId!.Equals(BaseData.NewUserKeyId!))
            {
                throw new BadRequestException("Invalid user key sent in in master-password unlock data.");
            }
            // else they match, so everything is correct
        } else
        {
            throw new BadRequestException("Invalid user key sent in in master-password unlock data.");
        }
    }
}
