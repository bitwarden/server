using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.UserKey.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.KeyManagement.UserKey.Models.Data;

public class BaseRotateUserAccountKeysDataTests
{
    private const string _storedUserKeyId = "0123456789abcdef0123456789abcdef";
    private const string _otherUserKeyId = "fedcba9876543210fedcba9876543210";

    [Theory]
    [BitAutoData]
    public void ValidateOldUserKeyIdForUser_NoStoredKeyIdAndNoOldUserKeyId_DoesNotThrow(User user)
    {
        user.SetUserKeyId(null);
        var model = CreateModel(oldUserKeyId: null);

        model.ValidateOldUserKeyIdForUser(user);
    }

    [Theory]
    [BitAutoData]
    public void ValidateOldUserKeyIdForUser_NoStoredKeyIdWithOldUserKeyId_DoesNotThrow(User user)
    {
        user.SetUserKeyId(null);
        var model = CreateModel(_otherUserKeyId);

        model.ValidateOldUserKeyIdForUser(user);
    }

    [Theory]
    [BitAutoData]
    public void ValidateOldUserKeyIdForUser_StoredKeyIdWithoutOldUserKeyId_DoesNotThrow(User user)
    {
        user.SetUserKeyId(KeyId.FromHexEncodedString(_storedUserKeyId));
        var model = CreateModel(oldUserKeyId: null);

        model.ValidateOldUserKeyIdForUser(user);
    }

    [Theory]
    [BitAutoData]
    public void ValidateOldUserKeyIdForUser_OldUserKeyIdMatchesStoredKeyId_DoesNotThrow(User user)
    {
        user.SetUserKeyId(KeyId.FromHexEncodedString(_storedUserKeyId));
        var model = CreateModel(_storedUserKeyId);

        model.ValidateOldUserKeyIdForUser(user);
    }

    [Theory]
    [BitAutoData]
    public void ValidateOldUserKeyIdForUser_OldUserKeyIdDoesNotMatchStoredKeyId_ThrowsConflictException(User user)
    {
        user.SetUserKeyId(KeyId.FromHexEncodedString(_storedUserKeyId));
        var model = CreateModel(_otherUserKeyId);

        Assert.Throws<ConflictException>(() => model.ValidateOldUserKeyIdForUser(user));
    }

    private static BaseRotateUserAccountKeysData CreateModel(string? oldUserKeyId) =>
        new()
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
            Sends = [],
            OldUserKeyId = KeyId.FromHexEncodedString(oldUserKeyId)
        };
}
