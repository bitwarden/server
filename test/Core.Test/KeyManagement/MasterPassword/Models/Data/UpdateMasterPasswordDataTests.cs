using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.KeyManagement.MasterPassword.Models.Data;

public class UpdateMasterPasswordDataTests
{
    private const string _mockAuthHash = "mockAuthenticationHash";
    private const string _mockMasterKeyWrappedUserKey = "mockMasterKeyWrappedUserKey";

    private static KdfSettings ValidKdf =>
        new() { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000, Memory = null, Parallelism = null };

    private static void SetupValidUser(User user)
    {
        user.Email = "test@example.com";
        user.MasterPasswordSalt = null;
        user.Kdf = ValidKdf.KdfType;
        user.KdfIterations = ValidKdf.Iterations;
        user.KdfMemory = ValidKdf.Memory;
        user.KdfParallelism = ValidKdf.Parallelism;
    }

    private static UpdateMasterPasswordData CreateValidModel(string salt, KdfSettings kdf) =>
        new()
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = kdf,
                MasterPasswordAuthenticationHash = _mockAuthHash,
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = kdf,
                MasterKeyWrappedUserKey = _mockMasterKeyWrappedUserKey,
                Salt = salt
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

        var model = new UpdateMasterPasswordData
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = ValidKdf,
                MasterPasswordAuthenticationHash = _mockAuthHash,
                Salt = "wrong@example.com"
            },
            MasterPasswordUnlock = validModel.MasterPasswordUnlock
        };

        Assert.Throws<InvalidOperationException>(() => model.ValidateForUser(user));
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_UnlockSaltMismatch_ThrowsInvalidOperationException(User user)
    {
        SetupValidUser(user);
        var validModel = CreateValidModel(user.Email, ValidKdf);

        var model = new UpdateMasterPasswordData
        {
            MasterPasswordAuthentication = validModel.MasterPasswordAuthentication,
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = ValidKdf,
                MasterKeyWrappedUserKey = _mockMasterKeyWrappedUserKey,
                Salt = "wrong@example.com"
            }
        };

        Assert.Throws<InvalidOperationException>(() => model.ValidateForUser(user));
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_AuthenticationKdfMismatch_ThrowsInvalidOperationException(User user)
    {
        SetupValidUser(user);
        var validModel = CreateValidModel(user.Email, ValidKdf);

        var model = new UpdateMasterPasswordData
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = new KdfSettings { KdfType = KdfType.Argon2id, Iterations = 3, Memory = 64, Parallelism = 4 },
                MasterPasswordAuthenticationHash = _mockAuthHash,
                Salt = validModel.MasterPasswordAuthentication.Salt
            },
            MasterPasswordUnlock = validModel.MasterPasswordUnlock
        };

        Assert.Throws<InvalidOperationException>(() => model.ValidateForUser(user));
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_UnlockKdfMismatch_ThrowsInvalidOperationException(User user)
    {
        SetupValidUser(user);
        var validModel = CreateValidModel(user.Email, ValidKdf);

        var model = new UpdateMasterPasswordData
        {
            MasterPasswordAuthentication = validModel.MasterPasswordAuthentication,
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = new KdfSettings { KdfType = KdfType.Argon2id, Iterations = 3, Memory = 64, Parallelism = 4 },
                MasterKeyWrappedUserKey = _mockMasterKeyWrappedUserKey,
                Salt = validModel.MasterPasswordUnlock.Salt
            }
        };

        Assert.Throws<InvalidOperationException>(() => model.ValidateForUser(user));
    }
}
