using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.UserFeatures.PremiumAccess;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
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
    [BitAutoData(TwoFactorProviderType.Authenticator)]
    [BitAutoData(TwoFactorProviderType.Email)]
    [BitAutoData(TwoFactorProviderType.Remember)]
    [BitAutoData(TwoFactorProviderType.OrganizationDuo)]
    [BitAutoData(TwoFactorProviderType.WebAuthn)]
    public async Task TwoFactorIsEnabledVNextAsync_WithProviderTypeNotRequiringPremium_ReturnsAllTwoFactorEnabled(
        TwoFactorProviderType freeProviderType,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        List<User> users)
    {
        // Arrange
        var userIds = users.Select(u => u.Id).ToList();
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { freeProviderType, new TwoFactorProvider { Enabled = true } }
        };

        foreach (var user in users)
        {
            user.Premium = false;
            user.SetTwoFactorProviders(twoFactorProviders);
        }

        var premiumStatus = users.ToDictionary(u => u.Id, u => false);

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(i => i.All(userIds.Contains)))
            .Returns(users);

        sutProvider.GetDependency<IPremiumAccessQuery>()
            .CanAccessPremiumAsync(Arg.Is<IEnumerable<User>>(u => u.All(users.Contains)))
            .Returns(premiumStatus);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(userIds);

        // Assert
        foreach (var user in users)
        {
            Assert.Contains(result, res => res.userId == user.Id && res.twoFactorIsEnabled == true);
        }
    }

    [Theory, BitAutoData]
    public async Task TwoFactorIsEnabledVNextAsync_DatabaseReturnsEmpty_ResultEmpty(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        List<User> users)
    {
        // Arrange
        var userIds = users.Select(u => u.Id).ToList();

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        sutProvider.GetDependency<IPremiumAccessQuery>()
            .CanAccessPremiumAsync(Arg.Any<IEnumerable<User>>())
            .Returns(new Dictionary<Guid, bool>());

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(userIds);

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData((IEnumerable<Guid>)null)]
    [BitAutoData([])]
    public async Task TwoFactorIsEnabledVNextAsync_UserIdsNullorEmpty_ResultEmpty(
        IEnumerable<Guid> userIds,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider)
    {
        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(userIds);

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData]
    public async Task TwoFactorIsEnabledVNextAsync_WithNoTwoFactorEnabled_ReturnsAllTwoFactorDisabled(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        List<User> users)
    {
        // Arrange
        var userIds = users.Select(u => u.Id).ToList();
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { TwoFactorProviderType.Email, new TwoFactorProvider { Enabled = false } }
        };

        foreach (var user in users)
        {
            user.SetTwoFactorProviders(twoFactorProviders);
        }

        var premiumStatus = users.ToDictionary(u => u.Id, u => false);

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(i => i.All(userIds.Contains)))
            .Returns(users);

        sutProvider.GetDependency<IPremiumAccessQuery>()
            .CanAccessPremiumAsync(Arg.Is<IEnumerable<User>>(u => u.All(users.Contains)))
            .Returns(premiumStatus);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(userIds);

        // Assert
        foreach (var user in users)
        {
            Assert.Contains(result, res => res.userId == user.Id && res.twoFactorIsEnabled == false);
        }
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    public async Task TwoFactorIsEnabledVNextAsync_WithProviderTypeRequiringPremium_ReturnsMixedResults(
        TwoFactorProviderType premiumProviderType,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        List<User> users)
    {
        // Arrange
        var userIds = users.Select(u => u.Id).ToList();
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { TwoFactorProviderType.Email, new TwoFactorProvider { Enabled = false } },
            { premiumProviderType, new TwoFactorProvider { Enabled = true } }
        };

        foreach (var user in users)
        {
            user.Premium = false;
            user.SetTwoFactorProviders(twoFactorProviders);
        }

        // Only the first user has premium access
        var premiumStatus = users.ToDictionary(
            u => u.Id,
            u => users.IndexOf(u) == 0);

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(i => i.All(userIds.Contains)))
            .Returns(users);

        sutProvider.GetDependency<IPremiumAccessQuery>()
            .CanAccessPremiumAsync(Arg.Is<IEnumerable<User>>(u => u.All(users.Contains)))
            .Returns(premiumStatus);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(userIds);

        // Assert
        foreach (var user in users)
        {
            var expectedEnabled = premiumStatus[user.Id];
            Assert.Contains(result, res => res.userId == user.Id && res.twoFactorIsEnabled == expectedEnabled);
        }
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("{}")]
    [BitAutoData((string)null)]
    public async Task TwoFactorIsEnabledVNextAsync_WithNullOrEmptyTwoFactorProviders_ReturnsAllTwoFactorDisabled(
        string twoFactorProviders,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        List<User> users)
    {
        // Arrange
        var userIds = users.Select(u => u.Id).ToList();

        foreach (var user in users)
        {
            user.TwoFactorProviders = twoFactorProviders;
        }

        var premiumStatus = users.ToDictionary(u => u.Id, u => false);

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(i => i.All(userIds.Contains)))
            .Returns(users);

        sutProvider.GetDependency<IPremiumAccessQuery>()
            .CanAccessPremiumAsync(Arg.Is<IEnumerable<User>>(u => u.All(users.Contains)))
            .Returns(premiumStatus);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(userIds);

        // Assert
        foreach (var user in users)
        {
            Assert.Contains(result, res => res.userId == user.Id && res.twoFactorIsEnabled == false);
        }
    }

    [Theory]
    [BitAutoData]
    public async Task TwoFactorIsEnabledVNextAsync_Generic_WithNoUserIds_ReturnsAllTwoFactorDisabled(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        List<OrganizationUserUserDetails> users)
    {
        // Arrange
        foreach (var user in users)
        {
            user.UserId = null;
        }

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(users);

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
    [BitAutoData]
    public async Task TwoFactorIsEnabledVNextAsync_SingleUser_UserIdNull_ReturnsFalse(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider)
    {
        // Arrange
        var user = new TestTwoFactorProviderUser
        {
            Id = null
        };

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(user);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Authenticator)]
    [BitAutoData(TwoFactorProviderType.Email)]
    [BitAutoData(TwoFactorProviderType.Remember)]
    [BitAutoData(TwoFactorProviderType.OrganizationDuo)]
    [BitAutoData(TwoFactorProviderType.WebAuthn)]
    public async Task TwoFactorIsEnabledVNextAsync_SingleUser_WithProviderTypeNotRequiringPremium_ReturnsTrue(
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
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(user);

        // Assert
        Assert.True(result);

        // Should not need to check premium access for free providers
        await sutProvider.GetDependency<IPremiumAccessQuery>()
            .DidNotReceiveWithAnyArgs()
            .CanAccessPremiumAsync(default(Guid), default);
    }

    [Theory]
    [BitAutoData]
    public async Task TwoFactorIsEnabledVNextAsync_SingleUser_WithNoTwoFactorEnabled_ReturnsFalse(
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
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(user);

        // Assert
        Assert.False(result);

        await sutProvider.GetDependency<IPremiumAccessQuery>()
            .DidNotReceiveWithAnyArgs()
            .CanAccessPremiumAsync(default(Guid), default);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    public async Task TwoFactorIsEnabledVNextAsync_SingleUser_WithProviderTypeRequiringPremium_WithoutPremium_ReturnsFalse(
        TwoFactorProviderType premiumProviderType,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        User user)
    {
        // Arrange
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { premiumProviderType, new TwoFactorProvider { Enabled = true } }
        };

        user.Premium = false;
        user.SetTwoFactorProviders(twoFactorProviders);

        sutProvider.GetDependency<IPremiumAccessQuery>()
            .CanAccessPremiumAsync(user.Id, false)
            .Returns(false);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(user);

        // Assert
        Assert.False(result);

        await sutProvider.GetDependency<IPremiumAccessQuery>()
            .Received(1)
            .CanAccessPremiumAsync(user.Id, false);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    public async Task TwoFactorIsEnabledVNextAsync_SingleUser_WithProviderTypeRequiringPremium_WithPersonalPremium_ReturnsTrue(
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

        sutProvider.GetDependency<IPremiumAccessQuery>()
            .CanAccessPremiumAsync(user.Id, true)
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(user);

        // Assert
        Assert.True(result);

        await sutProvider.GetDependency<IPremiumAccessQuery>()
            .Received(1)
            .CanAccessPremiumAsync(user.Id, true);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    public async Task TwoFactorIsEnabledVNextAsync_SingleUser_WithProviderTypeRequiringPremium_WithOrgPremium_ReturnsTrue(
        TwoFactorProviderType premiumProviderType,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        User user)
    {
        // Arrange
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { premiumProviderType, new TwoFactorProvider { Enabled = true } }
        };

        user.Premium = false;
        user.SetTwoFactorProviders(twoFactorProviders);

        sutProvider.GetDependency<IPremiumAccessQuery>()
            .CanAccessPremiumAsync(user.Id, false)
            .Returns(true); // Has premium from org

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(user);

        // Assert
        Assert.True(result);

        await sutProvider.GetDependency<IPremiumAccessQuery>()
            .Received(1)
            .CanAccessPremiumAsync(user.Id, false);
    }

    [Theory]
    [BitAutoData]
    public async Task TwoFactorIsEnabledVNextAsync_SingleUser_WithNullTwoFactorProviders_ReturnsFalse(
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        User user)
    {
        // Arrange
        user.TwoFactorProviders = null;

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(user);

        // Assert
        Assert.False(result);
        await sutProvider.GetDependency<IPremiumAccessQuery>()
            .DidNotReceiveWithAnyArgs()
            .CanAccessPremiumAsync(default(Guid), default);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    public async Task TwoFactorIsEnabledVNextAsync_SingleUser_OrganizationUserUserDetails_WithPremium_ReturnsTrue(
        TwoFactorProviderType premiumProviderType,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        OrganizationUserUserDetails orgUserDetails)
    {
        // Arrange
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { premiumProviderType, new TwoFactorProvider { Enabled = true } }
        };

        orgUserDetails.Premium = false;
        orgUserDetails.TwoFactorProviders = JsonHelpers.LegacySerialize(twoFactorProviders, JsonHelpers.LegacyEnumKeyResolver);

        sutProvider.GetDependency<IPremiumAccessQuery>()
            .CanAccessPremiumAsync(orgUserDetails.UserId!.Value, false)
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(orgUserDetails);

        // Assert
        Assert.True(result);

        await sutProvider.GetDependency<IPremiumAccessQuery>()
            .Received(1)
            .CanAccessPremiumAsync(orgUserDetails.UserId.Value, false);
    }

    [Theory]
    [BitAutoData(TwoFactorProviderType.Duo)]
    [BitAutoData(TwoFactorProviderType.YubiKey)]
    public async Task TwoFactorIsEnabledVNextAsync_SingleUser_UnknownType_FetchesUser(
        TwoFactorProviderType premiumProviderType,
        SutProvider<TwoFactorIsEnabledQuery> sutProvider,
        User fetchedUser)
    {
        // Arrange
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { premiumProviderType, new TwoFactorProvider { Enabled = true } }
        };

        var testUser = new TestTwoFactorProviderUser
        {
            Id = fetchedUser.Id,
            Premium = false,
            TwoFactorProviders = JsonHelpers.LegacySerialize(twoFactorProviders, JsonHelpers.LegacyEnumKeyResolver)
        };

        fetchedUser.Premium = false;

        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(fetchedUser.Id)
            .Returns(fetchedUser);

        sutProvider.GetDependency<IPremiumAccessQuery>()
            .CanAccessPremiumAsync(fetchedUser.Id, false)
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.TwoFactorIsEnabledVNextAsync(testUser);

        // Assert
        Assert.True(result);

        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .GetByIdAsync(fetchedUser.Id);

        await sutProvider.GetDependency<IPremiumAccessQuery>()
            .Received(1)
            .CanAccessPremiumAsync(fetchedUser.Id, false);
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
