using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.UserKey.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.KeyManagement.UserKey;

public class KeyConnectorRotateUserAccountKeysDataTests
{
    [Theory, BitAutoData]
    public void ValidateForUser_ValidKeyConnectorUser_DoesNotThrow(
        KeyConnectorRotateUserAccountKeysData data, User user)
    {
        user.Key = "encrypted-key";
        user.MasterPassword = null;
        user.UsesKeyConnector = true;

        var exception = Record.Exception(() => data.ValidateForUser(user));

        Assert.Null(exception);
    }

    [Theory]
    [BitAutoData([null, null, false])]
    [BitAutoData([null, null, true])]
    [BitAutoData([null, "hashedPassword", false])]
    [BitAutoData([null, "hashedPassword", true])]
    [BitAutoData("encrypted-key", "hashedPassword", false)]
    [BitAutoData("encrypted-key", "hashedPassword", true)]
    [BitAutoData("encrypted-key", null, false)]
    public void ValidateForUser_InvalidUserState_ThrowsBadRequestException(
        string? key, string? masterPassword, bool usesKeyConnector,
        KeyConnectorRotateUserAccountKeysData data, User user)
    {
        user.Key = key;
        user.MasterPassword = masterPassword;
        user.UsesKeyConnector = usesKeyConnector;

        Assert.Throws<BadRequestException>(() => data.ValidateForUser(user));
    }
}
