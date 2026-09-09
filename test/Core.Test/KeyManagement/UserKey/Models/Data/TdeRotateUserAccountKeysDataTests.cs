using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.UserKey.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.KeyManagement.UserKey.Models.Data;

public class TdeRotateUserAccountKeysDataTests
{
    private static TdeRotateUserAccountKeysData CreateValidModel() =>
    new()
    {
        BaseData = new BaseRotateUserAccountKeysData
        {
            AccountKeys = new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData =
                    new PublicKeyEncryptionKeyPairData("mockWrappedPrivateKey", "mockPublicKey")
            },
            EmergencyAccesses = [],
            OrganizationUsers = [],
            WebAuthnKeys = [],
            DeviceKeys = [],
            Ciphers = [],
            Folders = [],
            Sends = []
        }
    };

    [Theory]
    [BitAutoData]
    public void ValidateForUser_ValidTdeUser_DoesNotThrow(User user)
    {
        user.Key = null;
        user.MasterPassword = null;
        user.UsesKeyConnector = false;

        var model = CreateValidModel();
        model.ValidateForUser(user);
    }

    [Theory]
    [BitAutoData("encryptedKey", null, false)]
    [BitAutoData((string)null, "hashedPassword", false)]
    [BitAutoData((string)null, null, true)]
    public void ValidateForUser_BadState_ThrowsBadRequestException(string? key, string? masterPassword, bool usesKeyConnector, User user)
    {
        user.Key = key;
        user.MasterPassword = masterPassword;
        user.UsesKeyConnector = usesKeyConnector;

        var model = CreateValidModel();
        Assert.Throws<BadRequestException>(() => model.ValidateForUser(user));
    }
}
