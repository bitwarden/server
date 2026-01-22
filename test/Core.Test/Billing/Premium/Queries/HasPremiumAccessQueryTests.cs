using Bit.Core.Billing.Premium.Models;
using Bit.Core.Billing.Premium.Queries;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Premium.Queries;

[SutProviderCustomize]
public class HasPremiumAccessQueryTests
{
    [Theory, BitAutoData]
    public async Task HasPremiumAccessAsync_WhenUserHasPersonalPremium_ReturnsTrue(
        UserPremiumAccess user,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.PersonalPremium = true;
        user.OrganizationPremium = false;

        sutProvider.GetDependency<IUserRepository>()
            .GetPremiumAccessAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.HasPremiumAccessAsync(user.Id);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumAccessAsync_WhenUserHasNoPersonalPremiumButHasOrgPremium_ReturnsTrue(
        UserPremiumAccess user,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.PersonalPremium = false;
        user.OrganizationPremium = true; // Has org premium

        sutProvider.GetDependency<IUserRepository>()
            .GetPremiumAccessAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.HasPremiumAccessAsync(user.Id);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumAccessAsync_WhenUserHasNoPersonalPremiumAndNoOrgPremium_ReturnsFalse(
        UserPremiumAccess user,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.PersonalPremium = false;
        user.OrganizationPremium = false;

        sutProvider.GetDependency<IUserRepository>()
            .GetPremiumAccessAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.HasPremiumAccessAsync(user.Id);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumAccessAsync_WhenUserNotFound_ThrowsNotFoundException(
        Guid userId,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserRepository>()
            .GetPremiumAccessAsync(userId)
            .Returns((UserPremiumAccess?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.HasPremiumAccessAsync(userId));
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganizationAsync_WhenUserHasNoOrganizations_ReturnsFalse(
        UserPremiumAccess user,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.PersonalPremium = false;
        user.OrganizationPremium = false; // No premium from anywhere

        sutProvider.GetDependency<IUserRepository>()
            .GetPremiumAccessAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.HasPremiumFromOrganizationAsync(user.Id);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganizationAsync_WhenUserHasPremiumFromOrg_ReturnsTrue(
        UserPremiumAccess user,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.PersonalPremium = false; // No personal premium
        user.OrganizationPremium = true; // But has premium from org

        sutProvider.GetDependency<IUserRepository>()
            .GetPremiumAccessAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.HasPremiumFromOrganizationAsync(user.Id);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganizationAsync_WhenUserHasOnlyPersonalPremium_ReturnsFalse(
        UserPremiumAccess user,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.PersonalPremium = true; // Has personal premium
        user.OrganizationPremium = false; // Not in any org that grants premium

        sutProvider.GetDependency<IUserRepository>()
            .GetPremiumAccessAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.HasPremiumFromOrganizationAsync(user.Id);

        // Assert
        Assert.False(result); // Should return false because user is not in an org that grants premium
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganizationAsync_WhenUserHasBothPersonalAndOrgPremium_ReturnsTrue(
        UserPremiumAccess user,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.PersonalPremium = true; // Has personal premium
        user.OrganizationPremium = true; // Also in an org that grants premium

        sutProvider.GetDependency<IUserRepository>()
            .GetPremiumAccessAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.HasPremiumFromOrganizationAsync(user.Id);

        // Assert
        Assert.True(result); // Should return true because user IS in an org that grants premium (regardless of personal premium)
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganizationAsync_WhenUserNotFound_ThrowsNotFoundException(
        Guid userId,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserRepository>()
            .GetPremiumAccessAsync(userId)
            .Returns((UserPremiumAccess?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.HasPremiumFromOrganizationAsync(userId));
    }

    [Theory, BitAutoData]
    public async Task HasPremiumAccessAsync_Bulk_WhenEmptyList_ReturnsEmptyDictionary(
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        var userIds = new List<Guid>();

        sutProvider.GetDependency<IUserRepository>()
            .GetPremiumAccessByIdsAsync(userIds)
            .Returns(new List<UserPremiumAccess>());

        // Act
        var result = await sutProvider.Sut.HasPremiumAccessAsync(userIds);

        // Assert
        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumAccessAsync_Bulk_ReturnsCorrectStatus(
        UserPremiumAccess user1,
        UserPremiumAccess user2,
        UserPremiumAccess user3,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        user1.PersonalPremium = true;
        user1.OrganizationPremium = false;
        user2.PersonalPremium = false;
        user2.OrganizationPremium = false;
        user3.PersonalPremium = false;
        user3.OrganizationPremium = true;

        var users = new List<UserPremiumAccess> { user1, user2, user3 };
        var userIds = users.Select(u => u.Id).ToList();

        sutProvider.GetDependency<IUserRepository>()
            .GetPremiumAccessByIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(userIds)))
            .Returns(users);

        // Act
        var result = await sutProvider.Sut.HasPremiumAccessAsync(userIds);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.True(result[user1.Id]);  // Personal premium
        Assert.False(result[user2.Id]); // No premium
        Assert.True(result[user3.Id]);  // Organization premium
    }
}
