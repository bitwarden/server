using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Services.DiscountAudienceFilters;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Billing.Subscriptions.Repositories;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.Billing.Services;

[SutProviderCustomize]
public class SubscriptionDiscountServiceTests
{
    [Theory, BitAutoData]
    public async Task GetEligibleDiscountsAsync_NoActiveDiscounts_ReturnsEmpty(
        User user,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetActiveDiscountsAsync()
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.GetEligibleDiscountsAsync(user);

        // Assert
        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task GetEligibleDiscountsAsync_AllUsersDiscount_ReturnsDiscount(
        User user,
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        // Arrange
        discount.AudienceType = DiscountAudienceType.AllUsers;

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetActiveDiscountsAsync()
            .Returns([discount]);

        // Act
        var result = await sutProvider.Sut.GetEligibleDiscountsAsync(user);

        // Assert
        Assert.Contains(discount, result);
        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .DidNotReceive()
            .GetFilter(Arg.Any<DiscountAudienceType>());
    }

    [Theory, BitAutoData]
    public async Task GetEligibleDiscountsAsync_UserIsEligibleForDiscount_ReturnsDiscount(
        User user,
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        // Arrange
        discount.AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions;

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetActiveDiscountsAsync()
            .Returns([discount]);

        var filter = Substitute.For<IDiscountAudienceFilter>();
        filter.IsUserEligible(user, discount).Returns(true);
        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .GetFilter(DiscountAudienceType.UserHasNoPreviousSubscriptions)
            .Returns(filter);

        // Act
        var result = await sutProvider.Sut.GetEligibleDiscountsAsync(user);

        // Assert
        Assert.Contains(discount, result);
    }

    [Theory, BitAutoData]
    public async Task GetEligibleDiscountsAsync_UserIsIneligibleForDiscount_ReturnsEmpty(
        User user,
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        // Arrange
        discount.AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions;

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetActiveDiscountsAsync()
            .Returns([discount]);

        var filter = Substitute.For<IDiscountAudienceFilter>();
        filter.IsUserEligible(user, discount).Returns(false);
        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .GetFilter(DiscountAudienceType.UserHasNoPreviousSubscriptions)
            .Returns(filter);

        // Act
        var result = await sutProvider.Sut.GetEligibleDiscountsAsync(user);

        // Assert
        Assert.DoesNotContain(discount, result);
    }

    [Theory, BitAutoData]
    public async Task GetEligibleDiscountsAsync_NoFilterForAudienceType_ReturnsEmpty(
        User user,
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        // Arrange
        discount.AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions;

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetActiveDiscountsAsync()
            .Returns([discount]);

        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .GetFilter(DiscountAudienceType.UserHasNoPreviousSubscriptions)
            .ReturnsNull();

        // Act
        var result = await sutProvider.Sut.GetEligibleDiscountsAsync(user);

        // Assert
        Assert.DoesNotContain(discount, result);
    }

    [Theory, BitAutoData]
    public async Task GetEligibleDiscountsAsync_MixedDiscounts_ReturnsOnlyEligible(
        User user,
        SubscriptionDiscount allUsersDiscount,
        SubscriptionDiscount eligibleDiscount,
        SubscriptionDiscount ineligibleDiscount,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        // Arrange
        allUsersDiscount.AudienceType = DiscountAudienceType.AllUsers;
        eligibleDiscount.AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions;
        ineligibleDiscount.AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions;

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetActiveDiscountsAsync()
            .Returns([allUsersDiscount, eligibleDiscount, ineligibleDiscount]);

        var filter = Substitute.For<IDiscountAudienceFilter>();
        filter.IsUserEligible(user, eligibleDiscount).Returns(true);
        filter.IsUserEligible(user, ineligibleDiscount).Returns(false);
        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .GetFilter(DiscountAudienceType.UserHasNoPreviousSubscriptions)
            .Returns(filter);

        // Act
        var result = await sutProvider.Sut.GetEligibleDiscountsAsync(user);

        // Assert
        Assert.Contains(allUsersDiscount, result);
        Assert.Contains(eligibleDiscount, result);
        Assert.DoesNotContain(ineligibleDiscount, result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountEligibilityForUserAsync_CouponNotFound_ReturnsFalse(
        User user,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync("invalid")
            .ReturnsNull();

        // Act
        var result = await sutProvider.Sut.ValidateDiscountEligibilityForUserAsync(user, "invalid");

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountEligibilityForUserAsync_CouponFound_UserIsEligible_ReturnsTrue(
        User user,
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        // Arrange
        discount.AudienceType = DiscountAudienceType.AllUsers;

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(discount.StripeCouponId)
            .Returns(discount);

        // Act
        var result = await sutProvider.Sut.ValidateDiscountEligibilityForUserAsync(user, discount.StripeCouponId);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountEligibilityForUserAsync_CouponFound_UserIsNotEligible_ReturnsFalse(
        User user,
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        // Arrange
        discount.AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions;

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetByStripeCouponIdAsync(discount.StripeCouponId)
            .Returns(discount);

        var filter = Substitute.For<IDiscountAudienceFilter>();
        filter.IsUserEligible(user, discount).Returns(false);
        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .GetFilter(DiscountAudienceType.UserHasNoPreviousSubscriptions)
            .Returns(filter);

        // Act
        var result = await sutProvider.Sut.ValidateDiscountEligibilityForUserAsync(user, discount.StripeCouponId);

        // Assert
        Assert.False(result);
    }
}
