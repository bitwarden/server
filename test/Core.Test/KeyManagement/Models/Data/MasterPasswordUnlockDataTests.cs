using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Models.Data;

public class MasterPasswordUnlockDataTests
{
    private const string _keyIdA = "0123456789abcdef0123456789abcdef";
    private const string _keyIdB = "fedcba9876543210fedcba9876543210";

    private static MasterPasswordUnlockData MakeData(string? userKeyId) => new()
    {
        Kdf = new KdfSettings { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 },
        MasterKeyWrappedUserKey = "wrapped-user-key",
        Salt = "test@example.com",
        UserKeyId = KeyId.FromHexEncodedString(userKeyId)
    };

    private static User MakeUser(string? userKeyId) => new()
    {
        Id = Guid.NewGuid(),
        Email = "test@example.com",
        UserKeyId = userKeyId
    };

    [Fact]
    public void ValidateUserKeyUnchangedForUser_WhenKeyIdsMatch_DoesNotThrow()
    {
        MakeData(_keyIdA).ValidateUserKeyUnchangedForUser(MakeUser(_keyIdA));
    }

    [Fact]
    public void ValidateUserKeyUnchangedForUser_WhenKeyIdsDiffer_Throws()
    {
        var exception = Assert.Throws<BadRequestException>(
            () => MakeData(_keyIdB).ValidateUserKeyUnchangedForUser(MakeUser(_keyIdA)));

        Assert.Equal("Invalid user key id.", exception.Message);
    }

    [Fact]
    public void ValidateUserKeyUnchangedForUser_WhenServerHasNoKeyId_DoesNotThrow()
    {
        // Backfill case: nothing to compare against yet.
        MakeData(_keyIdA).ValidateUserKeyUnchangedForUser(MakeUser(null));
    }

    [Fact]
    public void ValidateUserKeyUnchangedForUser_WhenRequestOmitsKeyId_DoesNotThrow()
    {
        // Clients that predate the field must keep working.
        MakeData(null).ValidateUserKeyUnchangedForUser(MakeUser(_keyIdA));
    }

    [Fact]
    public void Equals_ComparesKeyIdByValue()
    {
        Assert.Equal(MakeData(_keyIdA), MakeData(_keyIdA));
        Assert.NotEqual(MakeData(_keyIdA), MakeData(_keyIdB));
        Assert.NotEqual(MakeData(_keyIdA), MakeData(null));
    }
}
