using Bit.Core.Auth.UserFeatures.PremiumAccess;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.PremiumAccess;

[SutProviderCustomize]
public class PremiumAccessQueryTests
{
    [Theory, BitAutoData]
    public async Task CanAccessPremiumAsync_WithUser_WhenUserHasPersonalPremium_ReturnsTrue(
        User user,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.Premium = true;

        // Act
        var result = await sutProvider.Sut.CanAccessPremiumAsync(user);

        // Assert
        Assert.True(result);

        // Should not call repository since personal premium is enough
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceive()
            .GetManyByUserAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task CanAccessPremiumAsync_WithUser_WhenUserHasNoPersonalPremiumButHasOrgPremium_ReturnsTrue(
        User user,
        OrganizationUser orgUser,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.Premium = false;
        orgUser.UserId = user.Id;

        var orgAbilities = new Dictionary<Guid, OrganizationAbility>
        {
            {
                orgUser.OrganizationId, new OrganizationAbility
                {
                    Id = orgUser.OrganizationId,
                    UsersGetPremium = true,
                    Enabled = true
                }
            }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns(new List<OrganizationUser> { orgUser });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(orgAbilities);

        // Act
        var result = await sutProvider.Sut.CanAccessPremiumAsync(user);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task CanAccessPremiumAsync_WithUser_WhenUserHasNoPersonalPremiumAndNoOrgPremium_ReturnsFalse(
        User user,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.Premium = false;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns(new List<OrganizationUser>());

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>());

        // Act
        var result = await sutProvider.Sut.CanAccessPremiumAsync(user);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task CanAccessPremiumAsync_WithGuidAndPremiumFlag_WhenHasPersonalPremium_ReturnsTrue(
        Guid userId,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Act
        var result = await sutProvider.Sut.CanAccessPremiumAsync(userId, hasPersonalPremium: true);

        // Assert
        Assert.True(result);

        // Should not call repository since personal premium is enough
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceive()
            .GetManyByUserAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task CanAccessPremiumAsync_WithGuidAndPremiumFlag_WhenNoPersonalPremiumButHasOrgPremium_ReturnsTrue(
        Guid userId,
        OrganizationUser orgUser,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        orgUser.UserId = userId;

        var orgAbilities = new Dictionary<Guid, OrganizationAbility>
        {
            {
                orgUser.OrganizationId, new OrganizationAbility
                {
                    Id = orgUser.OrganizationId,
                    UsersGetPremium = true,
                    Enabled = true
                }
            }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser> { orgUser });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(orgAbilities);

        // Act
        var result = await sutProvider.Sut.CanAccessPremiumAsync(userId, hasPersonalPremium: false);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task CanAccessPremiumAsync_WithGuidAndPremiumFlag_WhenNoPersonalPremiumAndNoOrgPremium_ReturnsFalse(
        Guid userId,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser>());

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>());

        // Act
        var result = await sutProvider.Sut.CanAccessPremiumAsync(userId, hasPersonalPremium: false);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganizationAsync_WhenUserHasNoOrganizations_ReturnsFalse(
        Guid userId,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser>());

        // Act
        var result = await sutProvider.Sut.HasPremiumFromOrganizationAsync(userId);

        // Assert
        Assert.False(result);

        // Should not call cache service if user has no organizations
        await sutProvider.GetDependency<IApplicationCacheService>()
            .DidNotReceive()
            .GetOrganizationAbilitiesAsync();
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganizationAsync_WhenOrgHasPremiumAndEnabled_ReturnsTrue(
        Guid userId,
        OrganizationUser orgUser,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        orgUser.UserId = userId;

        var orgAbilities = new Dictionary<Guid, OrganizationAbility>
        {
            {
                orgUser.OrganizationId, new OrganizationAbility
                {
                    Id = orgUser.OrganizationId,
                    UsersGetPremium = true,
                    Enabled = true
                }
            }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser> { orgUser });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(orgAbilities);

        // Act
        var result = await sutProvider.Sut.HasPremiumFromOrganizationAsync(userId);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganizationAsync_WhenOrgDoesNotHaveUsersGetPremium_ReturnsFalse(
        Guid userId,
        OrganizationUser orgUser,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        orgUser.UserId = userId;

        var orgAbilities = new Dictionary<Guid, OrganizationAbility>
        {
            {
                orgUser.OrganizationId, new OrganizationAbility
                {
                    Id = orgUser.OrganizationId,
                    UsersGetPremium = false, // No premium for users
                    Enabled = true
                }
            }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser> { orgUser });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(orgAbilities);

        // Act
        var result = await sutProvider.Sut.HasPremiumFromOrganizationAsync(userId);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganizationAsync_WhenOrgIsDisabled_ReturnsFalse(
        Guid userId,
        OrganizationUser orgUser,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        orgUser.UserId = userId;

        var orgAbilities = new Dictionary<Guid, OrganizationAbility>
        {
            {
                orgUser.OrganizationId, new OrganizationAbility
                {
                    Id = orgUser.OrganizationId,
                    UsersGetPremium = true,
                    Enabled = false // Organization disabled
                }
            }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser> { orgUser });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(orgAbilities);

        // Act
        var result = await sutProvider.Sut.HasPremiumFromOrganizationAsync(userId);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganizationAsync_WhenOrgNotInCache_ReturnsFalse(
        Guid userId,
        OrganizationUser orgUser,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        orgUser.UserId = userId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser> { orgUser });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>()); // Empty cache

        // Act
        var result = await sutProvider.Sut.HasPremiumFromOrganizationAsync(userId);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganizationAsync_WhenUserInMultipleOrgs_OnlyOneHasPremium_ReturnsTrue(
        Guid userId,
        OrganizationUser orgUser1,
        OrganizationUser orgUser2,
        OrganizationUser orgUser3,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        orgUser1.UserId = userId;
        orgUser2.UserId = userId;
        orgUser3.UserId = userId;

        var orgAbilities = new Dictionary<Guid, OrganizationAbility>
        {
            {
                orgUser1.OrganizationId, new OrganizationAbility
                {
                    Id = orgUser1.OrganizationId,
                    UsersGetPremium = false,
                    Enabled = true
                }
            },
            {
                orgUser2.OrganizationId, new OrganizationAbility
                {
                    Id = orgUser2.OrganizationId,
                    UsersGetPremium = true, // This one has premium
                    Enabled = true
                }
            },
            {
                orgUser3.OrganizationId, new OrganizationAbility
                {
                    Id = orgUser3.OrganizationId,
                    UsersGetPremium = false,
                    Enabled = true
                }
            }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser> { orgUser1, orgUser2, orgUser3 });

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(orgAbilities);

        // Act
        var result = await sutProvider.Sut.HasPremiumFromOrganizationAsync(userId);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task CanAccessPremiumBulkAsync_WhenEmptyUsersList_ReturnsEmptyDictionary(
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        var users = new List<User>();

        // Act
        var result = await sutProvider.Sut.CanAccessPremiumBulkAsync(users);

        // Assert
        Assert.Empty(result);

        // Should not call dependencies for empty list
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceive()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>());
    }

    [Theory, BitAutoData]
    public async Task CanAccessPremiumBulkAsync_WhenAllUsersHavePersonalPremium_ReturnsAllTrue(
        List<User> users,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        foreach (var user in users)
        {
            user.Premium = true;
        }

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser>());

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>());

        // Act
        var result = await sutProvider.Sut.CanAccessPremiumBulkAsync(users);

        // Assert
        Assert.Equal(users.Count, result.Count);
        foreach (var user in users)
        {
            Assert.True(result[user.Id]);
        }
    }

    [Theory, BitAutoData]
    public async Task CanAccessPremiumBulkAsync_WhenNoUsersHavePremium_ReturnsAllFalse(
        List<User> users,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        foreach (var user in users)
        {
            user.Premium = false;
        }

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser>());

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>());

        // Act
        var result = await sutProvider.Sut.CanAccessPremiumBulkAsync(users);

        // Assert
        Assert.Equal(users.Count, result.Count);
        foreach (var user in users)
        {
            Assert.False(result[user.Id]);
        }
    }

    [Theory, BitAutoData]
    public async Task CanAccessPremiumBulkAsync_WhenSomeUsersHaveOrgPremium_ReturnsCorrectStatus(
        User user1,
        User user2,
        User user3,
        OrganizationUser orgUser1,
        OrganizationUser orgUser2,
        Guid orgId,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        user1.Premium = false; // Will get premium from org
        user2.Premium = true;  // Has personal premium
        user3.Premium = false; // No premium at all

        orgUser1.UserId = user1.Id;
        orgUser1.OrganizationId = orgId;
        orgUser2.UserId = user3.Id;
        orgUser2.OrganizationId = orgId;

        var users = new List<User> { user1, user2, user3 };
        var orgUsers = new List<OrganizationUser> { orgUser1, orgUser2 };

        var orgAbilities = new Dictionary<Guid, OrganizationAbility>
        {
            {
                orgId, new OrganizationAbility
                {
                    Id = orgId,
                    UsersGetPremium = true,
                    Enabled = true
                }
            }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Contains(user1.Id) && ids.Contains(user2.Id) && ids.Contains(user3.Id)))
            .Returns(orgUsers);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(orgAbilities);

        // Act
        var result = await sutProvider.Sut.CanAccessPremiumBulkAsync(users);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.True(result[user1.Id]);  // Premium from org
        Assert.True(result[user2.Id]);  // Personal premium
        Assert.True(result[user3.Id]);  // Premium from org
    }

    [Theory, BitAutoData]
    public async Task CanAccessPremiumBulkAsync_WhenOrgUsersHaveNoUserId_FiltersThemOut(
        User user1,
        OrganizationUser orgUser1,
        OrganizationUser orgUser2,
        Guid orgId,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        user1.Premium = false;

        orgUser1.UserId = user1.Id;
        orgUser1.OrganizationId = orgId;
        orgUser2.UserId = null; // This should be filtered out
        orgUser2.OrganizationId = orgId;

        var users = new List<User> { user1 };
        var orgUsers = new List<OrganizationUser> { orgUser1, orgUser2 };

        var orgAbilities = new Dictionary<Guid, OrganizationAbility>
        {
            {
                orgId, new OrganizationAbility
                {
                    Id = orgId,
                    UsersGetPremium = true,
                    Enabled = true
                }
            }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(orgUsers);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(orgAbilities);

        // Act
        var result = await sutProvider.Sut.CanAccessPremiumBulkAsync(users);

        // Assert
        Assert.Single(result);
        Assert.True(result[user1.Id]);
    }

    [Theory, BitAutoData]
    public async Task CanAccessPremiumBulkAsync_WhenOrgIsDisabled_DoesNotGrantPremium(
        User user1,
        OrganizationUser orgUser1,
        Guid orgId,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        user1.Premium = false;
        orgUser1.UserId = user1.Id;
        orgUser1.OrganizationId = orgId;

        var users = new List<User> { user1 };
        var orgUsers = new List<OrganizationUser> { orgUser1 };

        var orgAbilities = new Dictionary<Guid, OrganizationAbility>
        {
            {
                orgId, new OrganizationAbility
                {
                    Id = orgId,
                    UsersGetPremium = true,
                    Enabled = false // Organization disabled
                }
            }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(orgUsers);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(orgAbilities);

        // Act
        var result = await sutProvider.Sut.CanAccessPremiumBulkAsync(users);

        // Assert
        Assert.Single(result);
        Assert.False(result[user1.Id]);
    }

    [Theory, BitAutoData]
    public async Task CanAccessPremiumBulkAsync_WhenOrgDoesNotHaveUsersGetPremium_DoesNotGrantPremium(
        User user1,
        OrganizationUser orgUser1,
        Guid orgId,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        user1.Premium = false;
        orgUser1.UserId = user1.Id;
        orgUser1.OrganizationId = orgId;

        var users = new List<User> { user1 };
        var orgUsers = new List<OrganizationUser> { orgUser1 };

        var orgAbilities = new Dictionary<Guid, OrganizationAbility>
        {
            {
                orgId, new OrganizationAbility
                {
                    Id = orgId,
                    UsersGetPremium = false, // Premium not available for users
                    Enabled = true
                }
            }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(orgUsers);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(orgAbilities);

        // Act
        var result = await sutProvider.Sut.CanAccessPremiumBulkAsync(users);

        // Assert
        Assert.Single(result);
        Assert.False(result[user1.Id]);
    }

    [Theory, BitAutoData]
    public async Task CanAccessPremiumBulkAsync_WhenUserInMultipleOrgs_OnlyOneHasPremium_GrantsPremium(
        User user1,
        OrganizationUser orgUser1,
        OrganizationUser orgUser2,
        Guid orgId1,
        Guid orgId2,
        SutProvider<PremiumAccessQuery> sutProvider)
    {
        // Arrange
        user1.Premium = false;

        orgUser1.UserId = user1.Id;
        orgUser1.OrganizationId = orgId1;
        orgUser2.UserId = user1.Id;
        orgUser2.OrganizationId = orgId2;

        var users = new List<User> { user1 };
        var orgUsers = new List<OrganizationUser> { orgUser1, orgUser2 };

        var orgAbilities = new Dictionary<Guid, OrganizationAbility>
        {
            {
                orgId1, new OrganizationAbility
                {
                    Id = orgId1,
                    UsersGetPremium = false,
                    Enabled = true
                }
            },
            {
                orgId2, new OrganizationAbility
                {
                    Id = orgId2,
                    UsersGetPremium = true, // This one grants premium
                    Enabled = true
                }
            }
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(orgUsers);

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(orgAbilities);

        // Act
        var result = await sutProvider.Sut.CanAccessPremiumBulkAsync(users);

        // Assert
        Assert.Single(result);
        Assert.True(result[user1.Id]);
    }
}
