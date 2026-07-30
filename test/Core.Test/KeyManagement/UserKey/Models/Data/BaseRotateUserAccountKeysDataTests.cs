using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.UserKey.Models.Data;
using Xunit;

namespace Bit.Core.Test.KeyManagement.UserKey.Models.Data;

public class BaseRotateUserAccountKeysDataTests
{
    private const string _keyIdA = "0123456789abcdef0123456789abcdef";
    private const string _keyIdB = "fedcba9876543210fedcba9876543210";

    private static BaseRotateUserAccountKeysData MakeData(string? rotationKeyId) => new()
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
        UserKeyId = KeyId.FromHexEncodedString(rotationKeyId)
    };

    [Fact]
    public void ValidateContainedKeyIdMatches_WhenKeyIdsMatch_DoesNotThrow()
    {
        MakeData(_keyIdA).ValidateContainedKeyIdMatches(KeyId.FromHexEncodedString(_keyIdA));
    }

    [Fact]
    public void ValidateContainedKeyIdMatches_WhenKeyIdsDiffer_Throws()
    {
        var exception = Assert.Throws<BadRequestException>(() =>
            MakeData(_keyIdA).ValidateContainedKeyIdMatches(KeyId.FromHexEncodedString(_keyIdB)));

        Assert.Equal("The user key id contained in the unlock data must match the user key id of the key rotation.",
            exception.Message);
    }

    [Fact]
    public void ValidateContainedKeyIdMatches_WhenContainedKeyIdIsAbsent_DoesNotThrow()
    {
        // Clients that predate the field carry no key id in the unlock data.
        MakeData(_keyIdA).ValidateContainedKeyIdMatches(null);
    }

    [Fact]
    public void ValidateContainedKeyIdMatches_WhenRotationKeyIdIsAbsent_Throws()
    {
        // Only the rotation's own key id is persisted, so a request that reports one solely in its
        // unlock data would silently leave the server with no key id for the new user key.
        Assert.Throws<BadRequestException>(() =>
            MakeData(null).ValidateContainedKeyIdMatches(KeyId.FromHexEncodedString(_keyIdA)));
    }

    [Fact]
    public void ValidateContainedKeyIdMatches_WhenNeitherKeyIdIsPresent_DoesNotThrow()
    {
        MakeData(null).ValidateContainedKeyIdMatches(null);
    }
}
