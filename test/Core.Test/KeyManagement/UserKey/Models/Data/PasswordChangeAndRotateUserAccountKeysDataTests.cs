using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.UserKey.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.KeyManagement.UserKey.Models.Data;

public class PasswordChangeAndRotateUserAccountKeysDataTests
{
    private const string _keyIdA = "0123456789abcdef0123456789abcdef";
    private const string _keyIdB = "fedcba9876543210fedcba9876543210";
    private const string _mockOldMasterKeyAuthenticationHash = "hash";
    private const string _mockMasterPasswordAuthenticationHash = "mockAuthenticationHash";
    private const string _mockMasterKeyWrappedUserKey = "mockMasterKeyWrappedUserKey";

    private static KdfSettings ValidKdf
    {
        get => new() { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000, Memory = null, Parallelism = null };
    }

    private static void SetupValidUser(User user)
    {
        user.Email = "test@example.com";
        user.MasterPasswordSalt = null;
        user.Kdf = ValidKdf.KdfType;
        user.KdfIterations = ValidKdf.Iterations;
        user.KdfMemory = ValidKdf.Memory;
        user.KdfParallelism = ValidKdf.Parallelism;
    }

    private static PasswordChangeAndRotateUserAccountKeysData CreateValidModel(string salt, KdfSettings kdf,
        string? unlockUserKeyId = null, string? rotationUserKeyId = null) =>
        new()
        {
            OldMasterKeyAuthenticationHash = _mockOldMasterKeyAuthenticationHash,
            MasterPasswordAuthenticationData =
                new MasterPasswordAuthenticationData
                {
                    Kdf = kdf,
                    MasterPasswordAuthenticationHash = _mockMasterPasswordAuthenticationHash,
                    Salt = salt
                },
            MasterPasswordUnlockData =
                new MasterPasswordUnlockData
                {
                    Kdf = kdf,
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
    public void ValidateForUser_ValidData_DoesNotThrow(User user)
    {
        SetupValidUser(user);
        var model = CreateValidModel(user.Email, ValidKdf);

        model.ValidateForUser(user);
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_UnlockKeyIdMatchesRotationKeyId_DoesNotThrow(User user)
    {
        SetupValidUser(user);
        var model = CreateValidModel(user.Email, ValidKdf, _keyIdA, _keyIdA);

        model.ValidateForUser(user);
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_UnlockKeyIdDiffersFromRotationKeyId_ThrowsBadRequestException(User user)
    {
        SetupValidUser(user);
        var model = CreateValidModel(user.Email, ValidKdf, _keyIdB, _keyIdA);

        Assert.Throws<BadRequestException>(() => model.ValidateForUser(user));
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_UnlockKeyIdWithoutRotationKeyId_ThrowsBadRequestException(User user)
    {
        // The rotation's key id is the only one persisted, so a request that reports one solely in
        // its unlock data must not pass as if the key id were tracked.
        SetupValidUser(user);
        var model = CreateValidModel(user.Email, ValidKdf, _keyIdA, null);

        Assert.Throws<BadRequestException>(() => model.ValidateForUser(user));
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_AuthenticationSaltMismatch_ThrowsInvalidOperationException(User user)
    {
        SetupValidUser(user);
        var validModel = CreateValidModel(user.Email, ValidKdf);

        var model = new PasswordChangeAndRotateUserAccountKeysData
        {
            OldMasterKeyAuthenticationHash = validModel.OldMasterKeyAuthenticationHash,
            MasterPasswordAuthenticationData = new MasterPasswordAuthenticationData
            {
                Kdf = ValidKdf,
                MasterPasswordAuthenticationHash = _mockMasterPasswordAuthenticationHash,
                Salt = "wrong@example.com"
            },
            MasterPasswordUnlockData = validModel.MasterPasswordUnlockData,
            BaseData = validModel.BaseData
        };

        Assert.Throws<InvalidOperationException>(() => model.ValidateForUser(user));
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_UnlockSaltMismatch_ThrowsInvalidOperationException(User user)
    {
        SetupValidUser(user);
        var validModel = CreateValidModel(user.Email, ValidKdf);

        var model = new PasswordChangeAndRotateUserAccountKeysData
        {
            OldMasterKeyAuthenticationHash = validModel.OldMasterKeyAuthenticationHash,
            MasterPasswordAuthenticationData = validModel.MasterPasswordAuthenticationData,
            MasterPasswordUnlockData = new MasterPasswordUnlockData
            {
                Kdf = ValidKdf,
                MasterKeyWrappedUserKey = _mockMasterKeyWrappedUserKey,
                Salt = "wrong@example.com"
            },
            BaseData = validModel.BaseData
        };

        Assert.Throws<InvalidOperationException>(() => model.ValidateForUser(user));
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_AuthenticationKdfMismatch_ThrowsInvalidOperationException(User user)
    {
        SetupValidUser(user);
        var validModel = CreateValidModel(user.Email, ValidKdf);

        var model = new PasswordChangeAndRotateUserAccountKeysData
        {
            OldMasterKeyAuthenticationHash = validModel.OldMasterKeyAuthenticationHash,
            MasterPasswordAuthenticationData = new MasterPasswordAuthenticationData
            {
                Kdf = new KdfSettings { KdfType = KdfType.Argon2id, Iterations = 3, Memory = 64, Parallelism = 4 },
                MasterPasswordAuthenticationHash =
                    validModel.MasterPasswordAuthenticationData.MasterPasswordAuthenticationHash,
                Salt = validModel.MasterPasswordAuthenticationData.Salt
            },
            MasterPasswordUnlockData = validModel.MasterPasswordUnlockData,
            BaseData = CreateValidModel(user.Email, ValidKdf).BaseData
        };

        Assert.Throws<InvalidOperationException>(() => model.ValidateForUser(user));
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_UnlockKdfMismatch_ThrowsInvalidOperationException(User user)
    {
        SetupValidUser(user);
        var validModel = CreateValidModel(user.Email, ValidKdf);

        var model = new PasswordChangeAndRotateUserAccountKeysData
        {
            OldMasterKeyAuthenticationHash = validModel.OldMasterKeyAuthenticationHash,
            MasterPasswordAuthenticationData = validModel.MasterPasswordAuthenticationData,
            MasterPasswordUnlockData = new MasterPasswordUnlockData
            {
                Kdf = new KdfSettings
                {
                    KdfType = KdfType.Argon2id,
                    Iterations = 3,
                    Memory = 64,
                    Parallelism = 4
                },
                MasterKeyWrappedUserKey = validModel.MasterPasswordUnlockData.MasterKeyWrappedUserKey,
                Salt = validModel.MasterPasswordUnlockData.Salt
            },
            BaseData = validModel.BaseData
        };

        Assert.Throws<InvalidOperationException>(() => model.ValidateForUser(user));
    }
}
