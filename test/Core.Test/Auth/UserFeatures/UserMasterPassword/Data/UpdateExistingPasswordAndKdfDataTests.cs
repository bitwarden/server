using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserMasterPassword.Data;

public class UpdateExistingPasswordAndKdfDataTests
{
    private static User BuildValidUser()
    {
        return new User
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
    }

    private static UpdateExistingPasswordAndKdfData BuildData(User user, string? saltOverride = null)
    {
        var salt = saltOverride ?? user.GetMasterPasswordSalt();
        var newKdf = new KdfSettings
        {
            KdfType = KdfType.Argon2id,
            Iterations = 3,
            Memory = 64,
            Parallelism = 4
        };
        return new UpdateExistingPasswordAndKdfData
        {
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = salt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = newKdf
            },
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = salt,
                MasterPasswordAuthenticationHash = "hash",
                Kdf = newKdf
            }
        };
    }

    [Fact]
    public void ValidateDataForUser_Accepts_WhenUserHasMasterPassword_SaltMatch_KdfChanged()
    {
        var user = BuildValidUser();
        var data = BuildData(user);

        // Should not throw — KDF change is permitted here
        data.ValidateDataForUser(user);
    }

    [Fact]
    public void ValidateDataForUser_Throws_WhenUserHasNoMasterPassword()
    {
        var user = BuildValidUser();
        user.MasterPassword = null;
        var data = BuildData(user);

        Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
    }

    [Fact]
    public void ValidateDataForUser_Throws_WhenUserIsKeyConnector()
    {
        var user = BuildValidUser();
        user.UsesKeyConnector = true;
        var data = BuildData(user);

        Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
    }

    [Fact]
    public void ValidateDataForUser_Throws_WhenSaltChanged()
    {
        var user = BuildValidUser();
        var data = BuildData(user, saltOverride: "wrong-salt");

        Assert.Throws<BadRequestException>(() => data.ValidateDataForUser(user));
    }
}
