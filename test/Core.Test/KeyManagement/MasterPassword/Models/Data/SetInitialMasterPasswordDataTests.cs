using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.KeyManagement.MasterPassword.Models.Data;

public class SetInitialMasterPasswordDataTests
{
    private const string _mockAuthHash = "mockAuthenticationHash";
    private const string _mockMasterKeyWrappedUserKey = "mockMasterKeyWrappedUserKey";

    private static KdfSettings ValidKdf =>
        new() { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000, Memory = null, Parallelism = null };

    private static void SetupValidUser(User user)
    {
        user.Email = "test@example.com";
        user.MasterPassword = null;
        user.Key = null;
        user.MasterPasswordSalt = null;
    }

    private static SetInitialMasterPasswordData CreateValidModel(string salt) =>
        new()
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Kdf = ValidKdf,
                MasterPasswordAuthenticationHash = _mockAuthHash,
                Salt = salt
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Kdf = ValidKdf,
                MasterKeyWrappedUserKey = _mockMasterKeyWrappedUserKey,
                Salt = salt
            }
        };

    [Theory]
    [BitAutoData]
    public void ValidateForUser_ValidData_DoesNotThrow(User user)
    {
        SetupValidUser(user);
        var model = CreateValidModel(user.Email);

        model.ValidateForUser(user);
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_UserAlreadyHasMasterPassword_ThrowsInvalidOperationException(User user)
    {
        SetupValidUser(user);
        user.MasterPassword = "existing-hash";
        var model = CreateValidModel(user.Email);

        Assert.Throws<InvalidOperationException>(() => model.ValidateForUser(user));
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_UserAlreadyHasKey_ThrowsInvalidOperationException(User user)
    {
        SetupValidUser(user);
        user.Key = "existing-key";
        var model = CreateValidModel(user.Email);

        Assert.Throws<InvalidOperationException>(() => model.ValidateForUser(user));
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_UserAlreadyHasSalt_ThrowsInvalidOperationException(User user)
    {
        SetupValidUser(user);
        user.MasterPasswordSalt = "existing-salt";
        var model = CreateValidModel(user.Email);

        Assert.Throws<InvalidOperationException>(() => model.ValidateForUser(user));
    }

    [Theory]
    [BitAutoData]
    public void ValidateForUser_AuthenticationSaltMismatch_ThrowsInvalidOperationException(User user)
    {
        SetupValidUser(user);
        var validModel = CreateValidModel(user.Email);

        var model = new SetInitialMasterPasswordData
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
        var validModel = CreateValidModel(user.Email);

        var model = new SetInitialMasterPasswordData
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
}
