using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.UserKey.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.KeyManagement.UserKey.Models.Data;

public class PasswordChangeAndRotateUserAccountKeysDataTests
{
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
        user.Kdf = ValidKdf.KdfType;
        user.KdfIterations = ValidKdf.Iterations;
        user.KdfMemory = ValidKdf.Memory;
        user.KdfParallelism = ValidKdf.Parallelism;
    }

    private static PasswordChangeAndRotateUserAccountKeysData CreateValidModel(string salt, KdfSettings kdf) =>
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
                    Salt = salt
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
                Sends = []
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
