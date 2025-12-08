using Bit.Core.Billing.Premium.Models;
using Bit.Core.Billing.Premium.Queries;
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
            .Returns<UserPremiumAccess>(_ => throw new Bit.Core.Exceptions.NotFoundException());

        // Act & Assert
        await Assert.ThrowsAsync<Bit.Core.Exceptions.NotFoundException>(
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
            .Returns<UserPremiumAccess>(_ => throw new Bit.Core.Exceptions.NotFoundException());

        // Act & Assert
        await Assert.ThrowsAsync<Bit.Core.Exceptions.NotFoundException>(
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
        List<UserPremiumAccess> users,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        users[0].PersonalPremium = true;
        users[0].OrganizationPremium = false;
        users[1].PersonalPremium = false;
        users[1].OrganizationPremium = false;
        users[2].PersonalPremium = false;
        users[2].OrganizationPremium = true;

        var userIds = users.Select(u => u.Id).ToList();

        sutProvider.GetDependency<IUserRepository>()
            .GetPremiumAccessByIdsAsync(userIds)
            .Returns(users);

        // Act
        var result = await sutProvider.Sut.HasPremiumAccessAsync(userIds);

        // Assert
        Assert.Equal(users.Count, result.Count);
        Assert.True(result[users[0].Id]);  // Personal premium
        Assert.False(result[users[1].Id]); // No premium
        Assert.True(result[users[2].Id]);  // Organization premium
    }
}


