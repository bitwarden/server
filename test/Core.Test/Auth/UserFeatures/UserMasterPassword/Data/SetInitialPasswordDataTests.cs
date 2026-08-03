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

    private static SetInitialPasswordData BuildData(User user, string? saltOverride = null,
        string? userKeyIdOverride = null)
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
                Kdf = kdf,
                UserKeyId = KeyId.FromHexEncodedString(userKeyIdOverride)
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

    [Fact]
    public void ValidateDataForUser_Throws_WhenUserKeyIdDisagreesWithStoredOne()
    {
        // A TDE or SSO JIT account holds a user key before it has a master password. Setting one
        // wraps that same key, so a request naming a different key is rejected rather than allowed
        // to rename the key the account is known to use.
        var user = BuildValidSetInitialUser();
        user.UserKeyId = "0123456789abcdef0123456789abcdef";
        var data = BuildData(user, userKeyIdOverride: "fedcba9876543210fedcba9876543210");

        var exception = Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
        Assert.Equal("Invalid user key id.", exception.Message);
    }

    [Fact]
    public void ValidateDataForUser_Accepts_WhenUserKeyIdMatchesStoredOne()
    {
        const string userKeyId = "0123456789abcdef0123456789abcdef";
        var user = BuildValidSetInitialUser();
        user.UserKeyId = userKeyId;
        var data = BuildData(user, userKeyIdOverride: userKeyId);

        // Should not throw
        data.ValidateDataForUser(user);
    }

    [Fact]
    public void ValidateDataForUser_Accepts_WhenAccountHasNoStoredUserKeyId()
    {
        // A freshly provisioned SSO JIT account, or a legacy one that has not been backfilled yet.
        var user = BuildValidSetInitialUser();
        user.UserKeyId = null;
        var data = BuildData(user, userKeyIdOverride: "fedcba9876543210fedcba9876543210");

        // Should not throw
        data.ValidateDataForUser(user);
    }

    [Fact]
    public void ValidateDataForUser_Accepts_WhenClientSuppliesNoUserKeyId()
    {
        var user = BuildValidSetInitialUser();
        user.UserKeyId = "0123456789abcdef0123456789abcdef";
        var data = BuildData(user);

        // Should not throw
        data.ValidateDataForUser(user);
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
