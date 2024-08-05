using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
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
    [BitAutoData]
    public async Task TwoFactorIsEnabledQuery_WithNullTwoFactorProviders_ReturnsAllTwoFactorDisabled(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        List<UserWithCalculatedPremium> usersWithCalculatedPremium)
    {
        // Arrange
        var userIds = usersWithCalculatedPremium.Select(u => u.Id).ToList();

        foreach (var user in usersWithCalculatedPremium)
        {
            user.TwoFactorProviders = null; // No two-factor providers configured
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

        user.Premium = false;
        user.SetTwoFactorProviders(twoFactorProviders);


        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(user);

        // Assert
        Assert.True(result);

        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyWithCalculatedPremiumAsync(default);
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
            .GetManyWithCalculatedPremiumAsync(default);
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
            .GetManyWithCalculatedPremiumAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(user.Id)))
            .Returns(new List<UserWithCalculatedPremium> { user });

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(user);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    public async Task TwoFactorIsEnabledQuery_WithProviderTypeRequiringPremium_WithUserPremium_ReturnsTrue(
        TwoFactorProviderType premiumProviderType,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        User user)
    {
        // Arrange
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { premiumProviderType, new TwoFactorProvider { Enabled = true } }
        };

        user.Premium = true;
        user.SetTwoFactorProviders(twoFactorProviders);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(user);

        // Assert
        Assert.True(result);

        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyWithCalculatedPremiumAsync(default);
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
            .GetManyWithCalculatedPremiumAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(user.Id)))
            .Returns(new List<UserWithCalculatedPremium> { user });

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledAsync(user);

        // Assert
        Assert.True(result);
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
    }
}
