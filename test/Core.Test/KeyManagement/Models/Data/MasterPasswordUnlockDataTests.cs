using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Models.Data;

public class MasterPasswordUnlockDataTests
{
    private const string StoredKeyId = "0123456789abcdef0123456789abcdef";
    private const string DifferentKeyId = "fedcba9876543210fedcba9876543210";

    private static User BuildUser(string? storedUserKeyId) => new()
    {
        Id = Guid.NewGuid(),
        Email = "test@example.com",
        Kdf = KdfType.PBKDF2_SHA256,
        KdfIterations = 600000,
        UserKeyId = storedUserKeyId
    };

    private static MasterPasswordUnlockData BuildUnlockData(string? suppliedUserKeyId) => new()
    {
        Salt = "test@example.com",
        MasterKeyWrappedUserKey = "wrapped-key",
        Kdf = new KdfSettings { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600000 },
        UserKeyId = KeyId.FromHexEncodedString(suppliedUserKeyId)
    };

    [Fact]
    public void ValidateUserKeyIdUnchangedForUser_Accepts_WhenNeitherSideHasAKeyId()
    {
        var user = BuildUser(null);

        BuildUnlockData(null).ValidateUserKeyIdUnchangedForUser(user);
    }

    [Fact]
    public void ValidateUserKeyIdUnchangedForUser_Accepts_WhenAccountHasNoKeyId()
    {
        // Nothing to disagree with. The supplied id is not recorded here either — backfilling a
        // legacy account belongs to TrySetUserKeyIdAsync, not to a password flow.
        var user = BuildUser(null);

        BuildUnlockData(DifferentKeyId).ValidateUserKeyIdUnchangedForUser(user);

        Assert.Null(user.UserKeyId);
    }

    [Fact]
    public void ValidateUserKeyIdUnchangedForUser_Accepts_WhenClientSuppliesNoKeyId()
    {
        // Clients predating the field send none; they must not be locked out of password changes.
        var user = BuildUser(StoredKeyId);

        BuildUnlockData(null).ValidateUserKeyIdUnchangedForUser(user);
    }

    [Fact]
    public void ValidateUserKeyIdUnchangedForUser_Accepts_WhenKeyIdsAgree()
    {
        var user = BuildUser(StoredKeyId);

        BuildUnlockData(StoredKeyId).ValidateUserKeyIdUnchangedForUser(user);
    }

    [Fact]
    public void ValidateUserKeyIdUnchangedForUser_Throws_WhenKeyIdsDisagree()
    {
        var user = BuildUser(StoredKeyId);

        var exception = Assert.Throws<BadRequestException>(
            () => BuildUnlockData(DifferentKeyId).ValidateUserKeyIdUnchangedForUser(user));

        Assert.Equal("Invalid user key id.", exception.Message);
    }

    [Fact]
    public void ValidateUserKeyIdUnchangedForUser_Accepts_WhenStoredKeyIdIsEmptyString()
    {
        // An empty column value is treated as unset rather than as a malformed key id, so a legacy
        // row does not fail every password change.
        var user = BuildUser(string.Empty);

        BuildUnlockData(DifferentKeyId).ValidateUserKeyIdUnchangedForUser(user);
    }

    [Fact]
    public void ValidateUserKeyIdUnchangedForUser_DoesNotMutateTheUser()
    {
        var user = BuildUser(StoredKeyId);

        BuildUnlockData(StoredKeyId).ValidateUserKeyIdUnchangedForUser(user);

        Assert.Equal(StoredKeyId, user.UserKeyId);
    }

    [Fact]
    public void Equals_ComparesKeyIdByValue()
    {
        Assert.Equal(BuildUnlockData(StoredKeyId), BuildUnlockData(StoredKeyId));
        Assert.NotEqual(BuildUnlockData(StoredKeyId), BuildUnlockData(DifferentKeyId));
        Assert.NotEqual(BuildUnlockData(StoredKeyId), BuildUnlockData(null));
    }
}
