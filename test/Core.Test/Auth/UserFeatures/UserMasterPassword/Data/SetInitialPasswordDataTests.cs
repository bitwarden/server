using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserMasterPassword.Data;

public class SetInitialPasswordDataTests
{
    private static User BuildValidSetInitialUser()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            MasterPassword = null,
            Key = null,
            MasterPasswordSalt = null,
            UsesKeyConnector = false,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 600000
        };
        return user;
    }

    private static SetInitialPasswordData BuildData(User user, string? saltOverride = null)
    {
        // Stage 1: salt == email while MasterPasswordSalt is null (PM-28143 separates them in Stage 3).
        var salt = saltOverride ?? user.GetMasterPasswordSalt();
        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        return new SetInitialPasswordData
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
    public void ValidateDataForUser_Accepts_WhenUserHasNoMasterPassword()
    {
        var user = BuildValidSetInitialUser();
        var data = BuildData(user);

        // Should not throw
        data.ValidateDataForUser(user);
    }

    [Fact]
    public void ValidateDataForUser_Throws_WhenUserHasMasterPassword()
    {
        var user = BuildValidSetInitialUser();
        user.MasterPassword = "existing-hash";
        var data = BuildData(user);

        Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
    }

    [Fact]
    public void ValidateDataForUser_Throws_WhenUserHasKey()
    {
        var user = BuildValidSetInitialUser();
        user.Key = "existing-key";
        var data = BuildData(user);

        Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
    }

    [Fact]
    public void ValidateDataForUser_Throws_WhenUserHasSalt()
    {
        var user = BuildValidSetInitialUser();
        user.MasterPasswordSalt = "existing-salt";
        var data = BuildData(user, saltOverride: "existing-salt");

        Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
    }

    [Fact]
    public void ValidateDataForUser_Throws_WhenUserIsKeyConnector()
    {
        var user = BuildValidSetInitialUser();
        user.UsesKeyConnector = true;
        var data = BuildData(user);

        Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
    }

    [Fact]
    public void ValidateDataForUser_Throws_WhenSaltMismatch()
    {
        var user = BuildValidSetInitialUser();
        var data = BuildData(user, saltOverride: "wrong-salt");

        Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
    }

    [Theory]
    [InlineData(true)]   // unlock salt wrong, authentication salt correct
    [InlineData(false)]  // unlock salt correct, authentication salt wrong
    public void ValidateDataForUser_Throws_WhenSaltMismatch_ValidatesBothFieldsIndependently(bool invalidateUnlockSaltInsteadOfAuthenticationSalt)
    {
        // One salt will always be invalid in these tests -- the flag signals which;
        // either/both should create an exceptional case.
        var user = BuildValidSetInitialUser();
        var correctSalt = user.GetMasterPasswordSalt();
        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        var data = new SetInitialPasswordData
        {
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = invalidateUnlockSaltInsteadOfAuthenticationSalt ? "wrong-salt" : correctSalt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = kdf
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = invalidateUnlockSaltInsteadOfAuthenticationSalt ? correctSalt : "wrong-salt",
                MasterPasswordAuthenticationHash = "hash",
                Kdf = kdf
            }
        };

        Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
    }
}
