using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.KeyManagement.UserKey.Models.Data;

public class KeyConnectorRotateUserAccountKeysData
{
    public required string KeyConnectorKeyWrappedUserKey { get; init; }
    public required BaseRotateUserAccountKeysData BaseData { get; init; }

    public void ValidateForUser(User user)
    {
        var isKeyConnectorUser = user is { Key: not null, MasterPassword: null, UsesKeyConnector: true };
        if (!isKeyConnectorUser)
        {
            throw new BadRequestException("User is in an invalid state for key connector key rotation.");
        }
    }
}
