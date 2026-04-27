using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserMasterPassword.Data;

public class UpdateExistingPasswordDataTests
{
    private static User BuildValidUpdateUser()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            MasterPassword = "existing-hash",
            Key = "existing-key",
            MasterPasswordSalt = "stored-salt",
            UsesKeyConnector = false,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 600000
        };
        return user;
    }

    private static UpdateExistingPasswordData BuildData(User user, string? saltOverride = null,
        KdfSettings? kdfOverride = null)
    {
        var salt = saltOverride ?? user.GetMasterPasswordSalt();
        var kdf = kdfOverride ?? new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        return new UpdateExistingPasswordData
        {
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = salt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = kdf
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = salt,
                MasterPasswordAuthenticationHash = "hash",
                Kdf = kdf
            }
        };
    }

    [Fact]
    public void ValidateDataForUser_Accepts_WhenUserHasMasterPassword_KdfAndSaltMatch()
    {
        var user = BuildValidUpdateUser();
        var data = BuildData(user);

        // Should not throw
        data.ValidateDataForUser(user);
    }

    [Fact]
    public void ValidateDataForUser_Throws_WhenUserHasNoMasterPassword()
    {
        var user = BuildValidUpdateUser();
        user.MasterPassword = null;
        var data = BuildData(user);

        Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
    }

    [Fact]
    public void ValidateDataForUser_Throws_WhenUserIsKeyConnector()
    {
        var user = BuildValidUpdateUser();
        user.UsesKeyConnector = true;
        var data = BuildData(user);

        Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
    }

    [Fact]
    public void ValidateDataForUser_Throws_WhenKdfChanged()
    {
        var user = BuildValidUpdateUser();
        var mismatchedKdf = new KdfSettings
        {
            KdfType = KdfType.Argon2id,
            Iterations = 3,
            Memory = 64,
            Parallelism = 4
        };
        var data = BuildData(user, kdfOverride: mismatchedKdf);

        Assert.Throws<ArgumentException>(() => data.ValidateDataForUser(user));
    }

    [Fact]
    public void ValidateDataForUser_Throws_WhenSaltChanged()
    {
        var user = BuildValidUpdateUser();
        var data = BuildData(user, saltOverride: "wrong-salt");

        Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
    }
}
