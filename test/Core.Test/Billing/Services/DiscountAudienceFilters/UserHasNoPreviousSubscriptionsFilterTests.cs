using Bit.Core.Billing.Services.DiscountAudienceFilters;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Billing.Services.DiscountAudienceFilters;

public class UserHasNoPreviousSubscriptionsFilterTests
{
    private readonly UserHasNoPreviousSubscriptionsFilter _sut = new();

    [Theory, BitAutoData]
    public void IsUserEligible_NotPremiumAndNoGatewaySubscriptionId_ReturnsTrue(
        User user,
        SubscriptionDiscount discount)
    {
        // Arrange
        user.Premium = false;
        user.GatewaySubscriptionId = null;

        // Act
        var result = _sut.IsUserEligible(user, discount);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public void IsUserEligible_HasPremium_ReturnsFalse(
        User user,
        SubscriptionDiscount discount)
    {
        // Arrange
        user.Premium = true;
        user.GatewaySubscriptionId = null;

        // Act
        var result = _sut.IsUserEligible(user, discount);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public void IsUserEligible_HasGatewaySubscriptionId_ReturnsFalse(
        User user,
        SubscriptionDiscount discount)
    {
        // Arrange
        user.Premium = false;

        // Act
        var result = _sut.IsUserEligible(user, discount);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public void IsUserEligible_HasPremiumAndHasGatewaySubscriptionId_ReturnsFalse(
        User user,
        SubscriptionDiscount discount)
    {
        // Arrange
        user.Premium = true;

        // Act
        var result = _sut.IsUserEligible(user, discount);

        // Assert
        Assert.False(result);
    }
}
