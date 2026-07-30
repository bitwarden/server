using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.UserKey.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.KeyManagement.UserKey.Models.Data;

public class MasterPasswordRotateUserAccountKeysDataTests
{
    private const string _keyIdA = "0123456789abcdef0123456789abcdef";
    private const string _keyIdB = "fedcba9876543210fedcba9876543210";
    private const string _mockMasterKeyWrappedUserKey = "mockMasterKeyWrappedUserKey";

    private static KdfSettings ValidKdf
    {
        get => new() { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000, Memory = null, Parallelism = null };
    }

    private static void SetupValidUser(User user)
    {
        user.Email = "test@example.com";
        user.MasterPasswordSalt = null;
        user.Key = "mockUserKey";
        user.MasterPassword = "mockMasterPasswordHash";
        user.Kdf = ValidKdf.KdfType;
        user.KdfIterations = ValidKdf.Iterations;
        user.KdfMemory = ValidKdf.Memory;
        user.KdfParallelism = ValidKdf.Parallelism;
    }

    private static MasterPasswordRotateUserAccountKeysData CreateModel(string salt, string? unlockUserKeyId,
        string? rotationUserKeyId) =>
        new()
        {
            MasterPasswordUnlockData = new MasterPasswordUnlockData
            {
                Kdf = ValidKdf,
                MasterKeyWrappedUserKey = _mockMasterKeyWrappedUserKey,
                Salt = salt,
                UserKeyId = KeyId.FromHexEncodedString(unlockUserKeyId)
            },
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
                Sends = [],
                UserKeyId = KeyId.FromHexEncodedString(rotationUserKeyId)
            }
        };

    [Theory]
    [BitAutoData]
    public void ValidateForUser_UnlockKeyIdMatchesRotationKeyId_DoesNotThrow(User user)
    {
        SetupValidUser(user);
        var model = CreateModel(user.Email, _keyIdA, _keyIdA);

        model.ValidateForUser(user);
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_NoKeyIds_DoesNotThrow(User user)
    {
        // Clients that predate the field send neither key id.
        SetupValidUser(user);
        var model = CreateModel(user.Email, null, null);

        model.ValidateForUser(user);
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_UnlockKeyIdDiffersFromRotationKeyId_ThrowsBadRequestException(User user)
    {
        SetupValidUser(user);
        var model = CreateModel(user.Email, _keyIdB, _keyIdA);

        Assert.Throws<BadRequestException>(() => model.ValidateForUser(user));
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_UnlockKeyIdWithoutRotationKeyId_ThrowsBadRequestException(User user)
    {
        // The rotation's key id is the only one persisted, so a request that reports one solely in
        // its unlock data must not pass as if the key id were tracked.
        SetupValidUser(user);
        var model = CreateModel(user.Email, _keyIdA, null);

        Assert.Throws<BadRequestException>(() => model.ValidateForUser(user));
    }
}
