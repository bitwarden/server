using Bit.Core.Billing.Premium.Queries;
using Bit.Core.Models.Data;
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
        UserWithCalculatedPremium user,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.Premium = true;
        user.HasPremiumAccess = true;

        sutProvider.GetDependency<IUserRepository>()
            .GetCalculatedPremiumAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.HasPremiumAccessAsync(user.Id);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumAccessAsync_WhenUserHasNoPersonalPremiumButHasOrgPremium_ReturnsTrue(
        UserWithCalculatedPremium user,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.Premium = false;
        user.HasPremiumAccess = true; // Has org premium

        sutProvider.GetDependency<IUserRepository>()
            .GetCalculatedPremiumAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.HasPremiumAccessAsync(user.Id);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumAccessAsync_WhenUserHasNoPersonalPremiumAndNoOrgPremium_ReturnsFalse(
        UserWithCalculatedPremium user,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.Premium = false;
        user.HasPremiumAccess = false;

        sutProvider.GetDependency<IUserRepository>()
            .GetCalculatedPremiumAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.HasPremiumAccessAsync(user.Id);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumAccessAsync_WhenUserNotFound_ReturnsFalse(
        Guid userId,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserRepository>()
            .GetCalculatedPremiumAsync(userId)
            .Returns((UserWithCalculatedPremium)null);

        // Act
        var result = await sutProvider.Sut.HasPremiumAccessAsync(userId);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganizationAsync_WhenUserHasNoOrganizations_ReturnsFalse(
        UserWithCalculatedPremium user,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.Premium = false;
        user.HasPremiumAccess = false; // No premium from anywhere

        sutProvider.GetDependency<IUserRepository>()
            .GetCalculatedPremiumAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.HasPremiumFromOrganizationAsync(user.Id);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganizationAsync_WhenUserHasPremiumFromOrg_ReturnsTrue(
        UserWithCalculatedPremium user,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.Premium = false; // No personal premium
        user.HasPremiumAccess = true; // But has premium from org

        sutProvider.GetDependency<IUserRepository>()
            .GetCalculatedPremiumAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.HasPremiumFromOrganizationAsync(user.Id);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganizationAsync_WhenUserHasOnlyPersonalPremium_ReturnsFalse(
        UserWithCalculatedPremium user,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        user.Premium = true; // Has personal premium
        user.HasPremiumAccess = true;

        sutProvider.GetDependency<IUserRepository>()
            .GetCalculatedPremiumAsync(user.Id)
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.HasPremiumFromOrganizationAsync(user.Id);

        // Assert
        Assert.False(result); // Should return false because premium is from personal, not org
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganizationAsync_WhenUserNotFound_ReturnsFalse(
        Guid userId,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserRepository>()
            .GetCalculatedPremiumAsync(userId)
            .Returns((UserWithCalculatedPremium)null);

        // Act
        var result = await sutProvider.Sut.HasPremiumFromOrganizationAsync(userId);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumAccessAsync_Bulk_WhenEmptyList_ReturnsEmptyDictionary(
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        var userIds = new List<Guid>();

        sutProvider.GetDependency<IUserRepository>()
            .GetManyWithCalculatedPremiumAsync(userIds)
            .Returns(new List<UserWithCalculatedPremium>());

        // Act
        var result = await sutProvider.Sut.HasPremiumAccessAsync(userIds);

        // Assert
        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task HasPremiumAccessAsync_Bulk_ReturnsCorrectStatus(
        List<UserWithCalculatedPremium> users,
        SutProvider<HasPremiumAccessQuery> sutProvider)
    {
        // Arrange
        users[0].HasPremiumAccess = true;
        users[1].HasPremiumAccess = false;
        users[2].HasPremiumAccess = true;

        var userIds = users.Select(u => u.Id).ToList();

        sutProvider.GetDependency<IUserRepository>()
            .GetManyWithCalculatedPremiumAsync(userIds)
            .Returns(users);

        // Act
        var result = await sutProvider.Sut.HasPremiumAccessAsync(userIds);

        // Assert
        Assert.Equal(users.Count, result.Count);
        Assert.True(result[users[0].Id]);
        Assert.False(result[users[1].Id]);
        Assert.True(result[users[2].Id]);
    }
}


