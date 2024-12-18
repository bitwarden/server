using Bit.Admin.Models;
using Bit.Core.Entities;
using Bit.Core.Vault.Entities;
using Bit.Test.Common.AutoFixture.Attributes;

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

        var actual = UserViewModel.MapViewModel(user, true, Array.Empty<Cipher>(), verifiedDomain);

        Assert.True(actual.ClaimedAccount);
    }

    [Theory]
    [BitAutoData]
    public void MapUserViewModel_WithoutVerifiedDomain_ReturnsUserViewModel(User user)
    {
        var verifiedDomain = false;

        var actual = UserViewModel.MapViewModel(user, true, Array.Empty<Cipher>(), verifiedDomain);

        Assert.False(actual.ClaimedAccount);
    }

    [Theory]
    [BitAutoData]
    public void MapUserViewModel_WithNullVerifiedDomain_ReturnsUserViewModel(User user)
    {
        var actual = UserViewModel.MapViewModel(user, true, Array.Empty<Cipher>(), null);

        Assert.Null(actual.ClaimedAccount);
    }
}
