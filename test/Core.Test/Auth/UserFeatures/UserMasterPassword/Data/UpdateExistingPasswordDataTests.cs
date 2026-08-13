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
    public void ValidateDataForUser_Accepts_WhenUserHasLegacyBelowMinimumPbkdf2Iterations()
    {
        // Legacy users created before the 600k minimum was enforced (Dec 2023) retain their
        // original iteration count. The password-only change path must accept their existing
        // KDF unchanged — ValidateUnchangedForUser mirrors the stored values; KdfSettingsValidator
        // is never called on this path.
        var user = BuildValidUpdateUser();
        user.KdfIterations = 100_000;
        var data = BuildData(user); // mirrors user's existing 100k KDF back

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

    [Theory]
    [InlineData(true)]   // unlock salt wrong, authentication salt correct
    [InlineData(false)]  // unlock salt correct, authentication salt wrong
    public void ValidateDataForUser_Throws_WhenSaltMismatch_ValidatesBothFieldsIndependently(bool invalidateUnlockSaltInsteadOfAuthenticationSalt)
    {
        // One salt will always be invalid in these tests -- the flag signals which;
        // either/both should create an exceptional case. 
        var user = BuildValidUpdateUser();
        var correctSalt = user.GetMasterPasswordSalt();
        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        var data = new UpdateExistingPasswordData
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

    [Theory]
    [InlineData(true)]   // unlock KDF wrong, authentication KDF correct
    [InlineData(false)]  // unlock KDF correct, authentication KDF wrong
    public void ValidateDataForUser_Throws_WhenKdfMismatch_ValidatesBothFieldsIndependently(bool unlockKdfIsWrong)
    {
        var user = BuildValidUpdateUser();
        var correctSalt = user.GetMasterPasswordSalt();
        var correctKdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        var wrongKdf = new KdfSettings { KdfType = KdfType.Argon2id, Iterations = 3, Memory = 64, Parallelism = 4 };
        var data = new UpdateExistingPasswordData
        {
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = correctSalt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = unlockKdfIsWrong ? wrongKdf : correctKdf
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = correctSalt,
                MasterPasswordAuthenticationHash = "hash",
                Kdf = unlockKdfIsWrong ? correctKdf : wrongKdf
            }
        };

        Assert.Throws<ArgumentException>(() => data.ValidateDataForUser(user));
    }
}
