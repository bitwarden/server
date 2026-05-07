using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.KeyManagement.UserKey.Models.Data;

public class TdeRotateUserAccountKeysData
{
    public required BaseRotateUserAccountKeysData BaseData { get; init; }

    public void ValidateForUser(User user)
    {
        var isTdeUser = user is { Key: null, MasterPassword: null, UsesKeyConnector: false };
        if (!isTdeUser)
        {
            throw new BadRequestException("User is in an invalid state for TDE key rotation.");
        }
    }
}
