using Bit.Core.Billing.Premium.Queries;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Premium.Queries;

public class PremiumAccessHelperTests
{
    [Theory, BitAutoData]
    public async Task CanAccessPremiumAsync_UserHasPersonalPremium_ReturnsTrueWithoutQueryingOrganizations(
        User user)
    {
        // Arrange
        user.Premium = true;
        var hasPremiumAccessQuery = Substitute.For<IHasPremiumAccessQuery>();

        // Act
        var result = await PremiumAccessHelper.CanAccessPremiumAsync(user, hasPremiumAccessQuery);

        // Assert
        Assert.True(result);
        await hasPremiumAccessQuery.DidNotReceiveWithAnyArgs().HasPremiumFromOrganizationAsync(default);
    }

    [Theory, BitAutoData]
    public async Task CanAccessPremiumAsync_UserHasNoPersonalPremiumButOrganizationGrantsIt_ReturnsTrue(
        User user)
    {
        // Arrange
        user.Premium = false;
        var hasPremiumAccessQuery = Substitute.For<IHasPremiumAccessQuery>();
        hasPremiumAccessQuery.HasPremiumFromOrganizationAsync(user.Id).Returns(true);

        // Act
        var result = await PremiumAccessHelper.CanAccessPremiumAsync(user, hasPremiumAccessQuery);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task CanAccessPremiumAsync_UserHasNoPremiumFromAnySource_ReturnsFalse(
        User user)
    {
        // Arrange
        user.Premium = false;
        var hasPremiumAccessQuery = Substitute.For<IHasPremiumAccessQuery>();
        hasPremiumAccessQuery.HasPremiumFromOrganizationAsync(user.Id).Returns(false);

        // Act
        var result = await PremiumAccessHelper.CanAccessPremiumAsync(user, hasPremiumAccessQuery);

        // Assert
        Assert.False(result);
    }
}
