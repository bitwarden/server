using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Billing.Premium.Queries;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.TwoFactorAuth;

[SutProviderCustomize]
public class TwoFactorIsEnabledQueryTests
{
    [Theory]
    [BitAutoData(TwoFactorProviderType.Authenticator)]
    [BitAutoData(TwoFactorProviderType.Email)]
    [BitAutoData(TwoFactorProviderType.Remember)]
    [BitAutoData(TwoFactorProviderType.OrganizationDuo)]
    [BitAutoData(TwoFactorProviderType.WebAuthn)]
    public async Task TwoFactorIsEnabledQuery_WithProviderTypeNotRequiringPremium_ReturnsAllTwoFactorEnabled(
        TwoFactorProviderType freeProviderType,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        List<UserWithCalculatedPremium> usersWithCalculatedPremium)
    {
        // Arrange
        var userIds = usersWithCalculatedPremium.Select(u => u.Id).ToList();
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { freeProviderType, new TwoFactorProvider { Enabled = true } } // Does not require premium
        };

        foreach (var user in usersWithCalculatedPremium)
        {
            user.HasPremiumAccess = false;
            user.SetTwoFactorProviders(twoFactorProviders);
        }

        sutProvider.GetDependency<IUserRepository>()
            .GetManyWithCalculatedPremiumAsync(Arg.Is<IEnumerable<Guid>>(i => i.All(userIds.Contains)))
            .Returns(usersWithCalculatedPremium);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(userIds);

        // Assert
        foreach (var userDetail in usersWithCalculatedPremium)
        {
            Assert.Contains(result, res => res.userId == userDetail.Id && res.twoFactorIsEnabled == true);
        }
    }

    [Theory, BitAutoData]
    public async Task TwoFactorIsEnabledQuery_DatabaseReturnsEmpty_ResultEmpty(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        List<UserWithCalculatedPremium> usersWithCalculatedPremium)
    {
        // Arrange
        var userIds = usersWithCalculatedPremium.Select(u => u.Id).ToList();

        sutProvider.GetDependency<IUserRepository>()
            .GetManyWithCalculatedPremiumAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(userIds);

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData((IEnumerable<Guid>)null)]
    [BitAutoData([])]
    public async Task TwoFactorIsEnabledQuery_UserIdsNullorEmpty_ResultEmpty(
    IEnumerable<Guid> userIds,
    SutProvider<TwoFactorIsEnabledQuery> sutProvider)
    {
        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(userIds);

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData]
    public async Task TwoFactorIsEnabledQuery_WithNoTwoFactorEnabled_ReturnsAllTwoFactorDisabled(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        List<UserWithCalculatedPremium> usersWithCalculatedPremium)
    {
        // Arrange
        var userIds = usersWithCalculatedPremium.Select(u => u.Id).ToList();
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { TwoFactorProviderType.Email, new TwoFactorProvider { Enabled = false } }
        };

        foreach (var user in usersWithCalculatedPremium)
        {
            user.SetTwoFactorProviders(twoFactorProviders);
        }

        sutProvider.GetDependency<IUserRepository>()
            .GetManyWithCalculatedPremiumAsync(Arg.Is<IEnumerable<Guid>>(i => i.All(userIds.Contains)))
            .Returns(usersWithCalculatedPremium);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(userIds);

        // Assert
        foreach (var userDetail in usersWithCalculatedPremium)
        {
            Assert.Contains(result, res => res.userId == userDetail.Id && res.twoFactorIsEnabled == false);
        }
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    public async Task TwoFactorIsEnabledQuery_WithProviderTypeRequiringPremium_ReturnsMixedResults(
        TwoFactorProviderType premiumProviderType,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        List<UserWithCalculatedPremium> usersWithCalculatedPremium)
    {
        // Arrange
        var userIds = usersWithCalculatedPremium.Select(u => u.Id).ToList();
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { TwoFactorProviderType.Email, new TwoFactorProvider { Enabled = false } },
            { premiumProviderType, new TwoFactorProvider { Enabled = true } }
        };

        foreach (var user in usersWithCalculatedPremium)
        {
            user.HasPremiumAccess = usersWithCalculatedPremium.IndexOf(user) == 0; // Only the first user has premium access
            user.SetTwoFactorProviders(twoFactorProviders);
        }

        sutProvider.GetDependency<IUserRepository>()
            .GetManyWithCalculatedPremiumAsync(Arg.Is<IEnumerable<Guid>>(i => i.All(userIds.Contains)))
            .Returns(usersWithCalculatedPremium);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(userIds);

        // Assert
        foreach (var userDetail in usersWithCalculatedPremium)
        {
            Assert.Contains(result, res => res.userId == userDetail.Id && res.twoFactorIsEnabled == userDetail.HasPremiumAccess);
        }
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("{}")]
    [BitAutoData((string)null)]
    public async Task TwoFactorIsEnabledQuery_WithNullOrEmptyTwoFactorProviders_ReturnsAllTwoFactorDisabled(
        string twoFactorProviders,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        List<UserWithCalculatedPremium> usersWithCalculatedPremium)
    {
        // Arrange
        var userIds = usersWithCalculatedPremium.Select(u => u.Id).ToList();

        foreach (var user in usersWithCalculatedPremium)
        {
            user.TwoFactorProviders = twoFactorProviders; // No two-factor providers configured
        }

        sutProvider.GetDependency<IUserRepository>()
            .GetManyWithCalculatedPremiumAsync(Arg.Is<IEnumerable<Guid>>(i => i.All(userIds.Contains)))
            .Returns(usersWithCalculatedPremium);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(userIds);

        // Assert
        foreach (var userDetail in usersWithCalculatedPremium)
        {
            Assert.Contains(result, res => res.userId == userDetail.Id && res.twoFactorIsEnabled == false);
        }
    }

    [Theory]
    [BitAutoData]
    public async Task TwoFactorIsEnabledQuery_WithNoUserIds_ReturnsAllTwoFactorDisabled(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        List<OrganizationUserUserDetails> users)
    {
        // Arrange
        foreach (var user in users)
        {
            user.UserId = null;
        }

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(users);

        // Assert
        foreach (var user in users)
        {
            Assert.Contains(result, res => res.user.Equals(user) && res.twoFactorIsEnabled == false);
        }

        // No UserIds were supplied so no calls to the UserRepository should have been made
        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyWithCalculatedPremiumAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task TwoFactorIsEnabledQuery_UserIdNull_ReturnsFalse(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider)
    {
        // Arrange
        var user = new TestTwoFactorProviderUser
        {
            Id = null
        };

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(user);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Authenticator)]
    [BitAutoData(TwoFactorProviderType.Email)]
    [BitAutoData(TwoFactorProviderType.Remember)]
    [BitAutoData(TwoFactorProviderType.OrganizationDuo)]
    [BitAutoData(TwoFactorProviderType.WebAuthn)]
    public async Task TwoFactorIsEnabledQuery_WithProviderTypeNotRequiringPremium_ReturnsTrue(
        TwoFactorProviderType freeProviderType,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        User user)
    {
        // Arrange
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { freeProviderType, new TwoFactorProvider { Enabled = true } }
        };

        user.SetTwoFactorProviders(twoFactorProviders);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(user);

        // Assert
        Assert.True(result);

        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetCalculatedPremiumAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task TwoFactorIsEnabledQuery_WithNoTwoFactorEnabled_ReturnsFalse(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        User user)
    {
        // Arrange
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { TwoFactorProviderType.Email, new TwoFactorProvider { Enabled = false } }
        };

        user.SetTwoFactorProviders(twoFactorProviders);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(user);

        // Assert
        Assert.False(result);

        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetCalculatedPremiumAsync(default);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    public async Task TwoFactorIsEnabledQuery_WithProviderTypeRequiringPremium_WithoutPremium_ReturnsFalse(
        TwoFactorProviderType premiumProviderType,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        UserWithCalculatedPremium user)
    {
        // Arrange
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { premiumProviderType, new TwoFactorProvider { Enabled = true } }
        };

        user.Premium = false;
        user.HasPremiumAccess = false;
        user.SetTwoFactorProviders(twoFactorProviders);

        sutProvider.GetDependency<IUserRepository>()
            .GetCalculatedPremiumAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(user);

        // Assert
        Assert.False(result);

        await sutProvider.GetDependency<IUserRepository>()
            .ReceivedWithAnyArgs(1)
            .GetCalculatedPremiumAsync(default);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    public async Task TwoFactorIsEnabledQuery_WithProviderTypeRequiringPremium_WithUserPremium_ReturnsTrue(
        TwoFactorProviderType premiumProviderType,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        UserWithCalculatedPremium user)
    {
        // Arrange
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { premiumProviderType, new TwoFactorProvider { Enabled = true } }
        };

        user.Premium = false;
        user.HasPremiumAccess = true;
        user.SetTwoFactorProviders(twoFactorProviders);

        sutProvider.GetDependency<IUserRepository>()
            .GetCalculatedPremiumAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(user);

        // Assert
        Assert.True(result);

        await sutProvider.GetDependency<IUserRepository>()
            .ReceivedWithAnyArgs(1)
            .GetCalculatedPremiumAsync(default);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    public async Task TwoFactorIsEnabledQuery_WithProviderTypeRequiringPremium_WithOrgPremium_ReturnsTrue(
        TwoFactorProviderType premiumProviderType,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        UserWithCalculatedPremium user)
    {
        // Arrange
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { premiumProviderType, new TwoFactorProvider { Enabled = true } }
        };

        user.Premium = false;
        user.HasPremiumAccess = true;
        user.SetTwoFactorProviders(twoFactorProviders);

        sutProvider.GetDependency<IUserRepository>()
            .GetCalculatedPremiumAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(user);

        // Assert
        Assert.True(result);

        await sutProvider.GetDependency<IUserRepository>()
            .ReceivedWithAnyArgs(1)
            .GetCalculatedPremiumAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task TwoFactorIsEnabledQuery_WithNullTwoFactorProviders_ReturnsFalse(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        User user)
    {
        // Arrange
        user.TwoFactorProviders = null; // No two-factor providers configured

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(user);

        // Assert
        Assert.False(result);
        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetCalculatedPremiumAsync(default);
    }

    [Theory]
    [BitAutoData((IEnumerable<Guid>)null)]
    [BitAutoData([])]
    public async Task TwoFactorIsEnabledAsync_WhenPremiumAccessQueryEnabled_WithNoUserIds_ReturnsEmpty(
        IEnumerable<Guid> userIds,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PremiumAccessQuery)
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(userIds);

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    public async Task TwoFactorIsEnabledAsync_WhenPremiumAccessQueryEnabled_WithMixedScenarios_ReturnsCorrectResults(
        TwoFactorProviderType premiumProviderType,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        User user1,
        User user2,
        User user3)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PremiumAccessQuery)
            .Returns(true);

        var users = new List<User> { user1, user2, user3 };
        var userIds = users.Select(u => u.Id).ToList();

        // User 1: Non-premium provider → 2FA enabled
        user1.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { TwoFactorProviderType.Authenticator, new TwoFactorProvider { Enabled = true } }
        });

        // User 2: Premium provider + has premium → 2FA enabled
        user2.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { premiumProviderType, new TwoFactorProvider { Enabled = true } }
        });

        // User 3: Premium provider + no premium → 2FA disabled
        user3.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { premiumProviderType, new TwoFactorProvider { Enabled = true } }
        });

        var premiumStatus = new Dictionary<Guid, bool>
        {
            { user2.Id, true },
            { user3.Id, false }
        };

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(userIds)))
            .Returns(users);

        sutProvider.GetDependency<IHasPremiumAccessQuery>()
            .HasPremiumAccessAsync(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Count() == 2 && ids.Contains(user2.Id) && ids.Contains(user3.Id)))
            .Returns(premiumStatus);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(userIds);

        // Assert
        Assert.Contains(result, res => res.userId == user1.Id && res.twoFactorIsEnabled == true);  // Non-premium provider
        Assert.Contains(result, res => res.userId == user2.Id && res.twoFactorIsEnabled == true);  // Premium + has premium
        Assert.Contains(result, res => res.userId == user3.Id && res.twoFactorIsEnabled == false); // Premium + no premium
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    public async Task TwoFactorIsEnabledAsync_WhenPremiumAccessQueryEnabled_OnlyChecksPremiumAccessForUsersWhoNeedIt(
        TwoFactorProviderType premiumProviderType,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        User user1,
        User user2,
        User user3)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PremiumAccessQuery)
            .Returns(true);

        var users = new List<User> { user1, user2, user3 };
        var userIds = users.Select(u => u.Id).ToList();

        // User 1: Has non-premium provider - should NOT trigger premium check
        user1.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { TwoFactorProviderType.Authenticator, new TwoFactorProvider { Enabled = true } }
        });

        // User 2 & 3: Have only premium providers - SHOULD trigger premium check
        user2.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { premiumProviderType, new TwoFactorProvider { Enabled = true } }
        });
        user3.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { premiumProviderType, new TwoFactorProvider { Enabled = true } }
        });

        var premiumStatus = new Dictionary<Guid, bool>
        {
            { user2.Id, true },
            { user3.Id, false }
        };

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(userIds)))
            .Returns(users);

        sutProvider.GetDependency<IHasPremiumAccessQuery>()
            .HasPremiumAccessAsync(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Count() == 2 && ids.Contains(user2.Id) && ids.Contains(user3.Id)))
            .Returns(premiumStatus);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(userIds);

        // Assert - Verify optimization: premium checked ONLY for users 2 and 3 (not user 1)
        await sutProvider.GetDependency<IHasPremiumAccessQuery>()
            .Received(1)
            .HasPremiumAccessAsync(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Count() == 2 && ids.Contains(user2.Id) && ids.Contains(user3.Id)));
    }

    [Theory]
    [BitAutoData]
    public async Task TwoFactorIsEnabledAsync_WhenPremiumAccessQueryEnabled_WithNoUserIds_ReturnsAllTwoFactorDisabled(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        List<OrganizationUserUserDetails> users)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PremiumAccessQuery)
            .Returns(true);

        foreach (var user in users)
        {
            user.UserId = null;
        }

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(users);

        // Assert
        foreach (var user in users)
        {
            Assert.Contains(result, res => res.user.Equals(user) && res.twoFactorIsEnabled == false);
        }

        // No UserIds were supplied so no calls to the UserRepository should have been made
        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyAsync(default);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Authenticator, true)]  // Non-premium provider
    [BitAutoData(TwoFactorProviderType.Duo, true)]            // Premium provider with premium access
    [BitAutoData(TwoFactorProviderType.YubiKey, false)]       // Premium provider without premium access
    public async Task TwoFactorIsEnabledAsync_WhenPremiumAccessQueryEnabled_SingleUser_VariousScenarios(
        TwoFactorProviderType providerType,
        bool hasPremiumAccess,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        User user)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PremiumAccessQuery)
            .Returns(true);

        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { providerType, new TwoFactorProvider { Enabled = true } }
        });

        sutProvider.GetDependency<IHasPremiumAccessQuery>()
            .HasPremiumAccessAsync(user.Id)
            .Returns(hasPremiumAccess);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(user);

        // Assert
        var requiresPremium = TwoFactorProvider.RequiresPremium(providerType);
        var expectedResult = !requiresPremium || hasPremiumAccess;
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [BitAutoData]
    public async Task TwoFactorIsEnabledAsync_WhenPremiumAccessQueryEnabled_WithNoEnabledProviders_ReturnsFalse(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        User user)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PremiumAccessQuery)
            .Returns(true);

        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { TwoFactorProviderType.Email, new TwoFactorProvider { Enabled = false } }
        });

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(user);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task TwoFactorIsEnabledAsync_WhenPremiumAccessQueryEnabled_WithNullProviders_ReturnsFalse(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        User user)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PremiumAccessQuery)
            .Returns(true);

        user.TwoFactorProviders = null;

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(user);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task TwoFactorIsEnabledAsync_WhenPremiumAccessQueryEnabled_UserNotFound_ThrowsNotFoundException(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        Guid userId)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PremiumAccessQuery)
            .Returns(true);

        var testUser = new TestTwoFactorProviderUser
        {
            Id = userId,
            TwoFactorProviders = null
        };

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(userId)
            .Returns((User)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.TwoFactorIsEnabledAsync(testUser));
    }

    private class TestTwoFactorProviderUser : ITwoFactorProvidersUser
    {
        public Guid? Id { get; set; }
        public string TwoFactorProviders { get; set; }
        public bool Premium { get; set; }
        public Dictionary<TwoFactorProviderType, TwoFactorProvider> GetTwoFactorProviders()
        {
            return JsonHelpers.LegacyDeserialize<Dictionary<TwoFactorProviderType, TwoFactorProvider>>(TwoFactorProviders);
        }

        public Guid? GetUserId()
        {
            return Id;
        }
    }
}
