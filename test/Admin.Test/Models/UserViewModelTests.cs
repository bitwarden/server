using Bit.Admin.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Vault.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Constants;

namespace Admin.Test.Models;

public class UserViewModelTests
{
    [Theory]
    [BitAutoData]
    public void IsTwoFactorEnabled_GivenUserAndIsInLookup_WhenUserHasTwoFactorEnabled_ThenReturnsTrue(User user)
    {
        var lookup = new List<(Guid, bool)>
        {
            (user.Id, true)
        };

        var actual = UserViewModel.IsTwoFactorEnabled(user, lookup);

        Assert.True(actual);
    }

    [Theory]
    [BitAutoData]
    public void IsTwoFactorEnabled_GivenUserAndIsInLookup_WhenUserDoesNotHaveTwoFactorEnabled_ThenReturnsFalse(User user)
    {
        var lookup = new List<(Guid, bool)>
        {
            (Guid.NewGuid(), true)
        };

        var actual = UserViewModel.IsTwoFactorEnabled(user, lookup);

        Assert.False(actual);
    }

    [Theory]
    [BitAutoData]
    public void IsTwoFactorEnabled_GivenUserAndIsNotInLookup_WhenUserDoesNotHaveTwoFactorEnabled_ThenReturnsFalse(User user)
    {
        var lookup = new List<(Guid, bool)>();

        var actual = UserViewModel.IsTwoFactorEnabled(user, lookup);

        Assert.False(actual);
    }

    [Theory]
    [BitAutoData]
    public void MapUserViewModel_GivenUser_WhenPopulated_ThenMapsToUserViewModel(User user)
    {
        var actual = UserViewModel.MapViewModel(user, true);

        Assert.Equal(actual.Id, user.Id);
        Assert.Equal(actual.Email, user.Email);
        Assert.Equal(actual.CreationDate, user.CreationDate);
        Assert.Equal(actual.PremiumExpirationDate, user.PremiumExpirationDate);
        Assert.Equal(actual.Premium, user.Premium);
        Assert.Equal(actual.MaxStorageGb, user.MaxStorageGb);
        Assert.Equal(actual.EmailVerified, user.EmailVerified);
        Assert.True(actual.TwoFactorEnabled);
        Assert.Equal(actual.AccountRevisionDate, user.AccountRevisionDate);
        Assert.Equal(actual.RevisionDate, user.RevisionDate);
        Assert.Equal(actual.LastEmailChangeDate, user.LastEmailChangeDate);
        Assert.Equal(actual.LastKdfChangeDate, user.LastKdfChangeDate);
        Assert.Equal(actual.LastKeyRotationDate, user.LastKeyRotationDate);
        Assert.Equal(actual.LastPasswordChangeDate, user.LastPasswordChangeDate);
        Assert.Equal(actual.Gateway, user.Gateway);
        Assert.Equal(actual.GatewayCustomerId, user.GatewayCustomerId);
        Assert.Equal(actual.GatewaySubscriptionId, user.GatewaySubscriptionId);
        Assert.Equal(actual.LicenseKey, user.LicenseKey);
    }

    [Theory]
    [BitAutoData]
    public void MapUserViewModel_GivenUserWithTwoFactorEnabled_WhenPopulated_ThenMapsToUserViewModel(User user)
    {
        var lookup = new List<(Guid, bool)> { (user.Id, true) };

        var actual = UserViewModel.MapViewModel(user, lookup, false);

        Assert.True(actual.TwoFactorEnabled);
    }

    [Theory]
    [BitAutoData]
    public void MapUserViewModel_GivenUserWithoutTwoFactorEnabled_WhenPopulated_ThenTwoFactorIsEnabled(User user)
    {
        var lookup = new List<(Guid, bool)> { (user.Id, false) };

        var actual = UserViewModel.MapViewModel(user, lookup, false);

        Assert.False(actual.TwoFactorEnabled);
    }

    [Theory]
    [BitAutoData]
    public void MapUserViewModel_GivenUser_WhenNotInLookUpList_ThenTwoFactorIsDisabled(User user)
    {
        var lookup = new List<(Guid, bool)> { (Guid.NewGuid(), true) };

        var actual = UserViewModel.MapViewModel(user, lookup, false);

        Assert.False(actual.TwoFactorEnabled);
    }

    [Theory]
    [BitAutoData]
    public void MapUserViewModel_WithVerifiedDomain_ReturnsUserViewModel(User user)
    {

        var verifiedDomain = true;

        var actual = UserViewModel.MapViewModel(user, true, Array.Empty<Cipher>(), verifiedDomain, false);

        Assert.True(actual.ClaimedAccount);
    }

    [Theory]
    [BitAutoData]
    public void MapUserViewModel_WithoutVerifiedDomain_ReturnsUserViewModel(User user)
    {
        var verifiedDomain = false;

        var actual = UserViewModel.MapViewModel(user, true, Array.Empty<Cipher>(), verifiedDomain, false);

        Assert.False(actual.ClaimedAccount);
    }

    [Theory]
    [BitAutoData]
    public void MapUserViewModel_WithNullVerifiedDomain_ReturnsUserViewModel(User user)
    {
        var actual = UserViewModel.MapViewModel(user, true, Array.Empty<Cipher>(), null, false);

        Assert.Null(actual.ClaimedAccount);
    }

    [Fact]
    public void Crypto_NoUserKeyId_UserKeyIdIsNull()
    {
        var user = new User { UserKeyId = null };

        var actual = UserViewModel.MapViewModel(user, true, Array.Empty<Cipher>(), null, false);

        Assert.Null(actual.Crypto.UserKeyId);
    }

    [Fact]
    public void Crypto_UserKeyIdSet_ReturnsKeyId()
    {
        var user = new User { UserKeyId = "0123456789abcdef0123456789abcdef" };

        var actual = UserViewModel.MapViewModel(user, true, Array.Empty<Cipher>(), null, false);

        Assert.Equal(user.UserKeyId, actual.Crypto.UserKeyId);
    }

    [Theory]
    [InlineData(2, TestEncryptionConstants.V2PrivateKey, true)]
    [InlineData(null, TestEncryptionConstants.V2PrivateKey, false)]
    [InlineData(2, TestEncryptionConstants.AES256_CBC_HMAC_Encstring, false)]
    public void Crypto_UsesV2Keys_MatchesHasV2Encryption(int? securityVersion, string privateKey, bool expected)
    {
        var user = new User { SecurityVersion = securityVersion, PrivateKey = privateKey };

        var actual = UserViewModel.MapViewModel(user, true, Array.Empty<Cipher>(), null, false);

        Assert.Equal(expected, actual.Crypto.UsesV2Keys);
    }

    [Theory]
    [InlineData("{}", true)]
    [InlineData(null, false)]
    public void Crypto_RotatedFromV1_TrueWhenV2UpgradeTokenPresent(string? token, bool expected)
    {
        var user = new User { V2UpgradeToken = token };

        var actual = UserViewModel.MapViewModel(user, true, Array.Empty<Cipher>(), null, false);

        Assert.Equal(expected, actual.Crypto.LastRotationFromV1);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(-1, false)]
    public void Crypto_RotatedWithPasswordChange_RequiresEqualTimestamps(int offsetSeconds, bool expected)
    {
        var rotation = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            LastKeyRotationDate = rotation,
            LastPasswordChangeDate = rotation.AddSeconds(offsetSeconds),
        };

        var actual = UserViewModel.MapViewModel(user, true, Array.Empty<Cipher>(), null, false);

        Assert.Equal(expected, actual.Crypto.KeyRotatedWithPasswordChange);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Crypto_RotatedWithPasswordChange_FalseWhenADateIsMissing(bool hasRotation, bool hasPasswordChange)
    {
        var now = DateTime.UtcNow;
        var user = new User
        {
            LastKeyRotationDate = hasRotation ? now : null,
            LastPasswordChangeDate = hasPasswordChange ? now : null,
        };

        var actual = UserViewModel.MapViewModel(user, true, Array.Empty<Cipher>(), null, false);

        Assert.False(actual.Crypto.KeyRotatedWithPasswordChange);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Crypto_NeedsRecoveryKeyV2Update_PassedThrough(bool needsUpdate)
    {
        var actual = UserViewModel.MapViewModel(new User(), true, Array.Empty<Cipher>(), null, needsUpdate);

        Assert.Equal(needsUpdate, actual.Crypto.NeedsRecoveryKeyV2Update);
    }

    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 599_999, true)]
    [InlineData(KdfType.PBKDF2_SHA256, 600_000, false)]
    [InlineData(KdfType.PBKDF2_SHA256, 1_000_000, false)]
    [InlineData(KdfType.Argon2id, 1, false)]
    public void Crypto_NeedsKdfUpdate_OnlyLowPbkdf2(KdfType kdf, int iterations, bool expected)
    {
        var user = new User { Kdf = kdf, KdfIterations = iterations };

        var actual = UserViewModel.MapViewModel(user, true, Array.Empty<Cipher>(), null, false);

        Assert.Equal(expected, actual.Crypto.NeedsKdfUpdate);
    }
}
