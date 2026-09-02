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
    private static IDictionary<DiscountTierType, bool> DiscountDictionary(bool eligibilitySetting)
        => Enum.GetValues<DiscountTierType>().ToDictionary(t => t, _ => eligibilitySetting);

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

        var filter = Substitute.For<IDiscountAudienceFilter>();
        filter.IsUserEligible(user, discount).Returns(DiscountDictionary(true));
        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .GetFilter(DiscountAudienceType.AllUsers)
            .Returns(filter);

        // Act
        var result = await sutProvider.Sut.GetEligibleDiscountsAsync(user);

        // Assert
        Assert.Contains(result, e => e.Discount == discount);
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
        filter.IsUserEligible(user, discount).Returns(DiscountDictionary(true));
        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .GetFilter(DiscountAudienceType.UserHasNoPreviousSubscriptions)
            .Returns(filter);

        // Act
        var result = await sutProvider.Sut.GetEligibleDiscountsAsync(user);

        // Assert
        Assert.Contains(result, e => e.Discount == discount);
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
        filter.IsUserEligible(user, discount).Returns(DiscountDictionary(false));
        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .GetFilter(DiscountAudienceType.UserHasNoPreviousSubscriptions)
            .Returns(filter);

        // Act
        var result = await sutProvider.Sut.GetEligibleDiscountsAsync(user);

        // Assert
        Assert.DoesNotContain(result, e => e.Discount == discount);
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
        Assert.DoesNotContain(result, e => e.Discount == discount);
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

        var allUsersFilter = Substitute.For<IDiscountAudienceFilter>();
        allUsersFilter.IsUserEligible(user, allUsersDiscount).Returns(DiscountDictionary(true));
        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .GetFilter(DiscountAudienceType.AllUsers)
            .Returns(allUsersFilter);

        var filter = Substitute.For<IDiscountAudienceFilter>();
        filter.IsUserEligible(user, eligibleDiscount).Returns(DiscountDictionary(true));
        filter.IsUserEligible(user, ineligibleDiscount).Returns(DiscountDictionary(false));
        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .GetFilter(DiscountAudienceType.UserHasNoPreviousSubscriptions)
            .Returns(filter);

        // Act
        var result = await sutProvider.Sut.GetEligibleDiscountsAsync(user);

        // Assert
        Assert.Contains(result, e => e.Discount == allUsersDiscount);
        Assert.Contains(result, e => e.Discount == eligibleDiscount);
        Assert.DoesNotContain(result, e => e.Discount == ineligibleDiscount);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountEligibilityForUserAsync_CouponNotInEligibleDiscounts_ReturnsFalse(
        User user,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        // Arrange — no active discounts, so the requested coupon won't be found
        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetActiveDiscountsAsync()
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.ValidateDiscountEligibilityForUserAsync(user, ["invalid"], DiscountTierType.Premium);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountEligibilityForUserAsync_CouponFound_TierEligible_ReturnsTrue(
        User user,
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        // Arrange
        discount.AudienceType = DiscountAudienceType.AllUsers;
        discount.StartDate = DateTime.UtcNow.AddDays(-1);
        discount.EndDate = DateTime.UtcNow.AddDays(30);

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetActiveDiscountsAsync()
            .Returns([discount]);

        var filter = Substitute.For<IDiscountAudienceFilter>();
        filter.IsUserEligible(user, discount).Returns(DiscountDictionary(true));
        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .GetFilter(DiscountAudienceType.AllUsers)
            .Returns(filter);

        // Act
        var result = await sutProvider.Sut.ValidateDiscountEligibilityForUserAsync(
            user, [discount.StripeCouponId], DiscountTierType.Premium);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountEligibilityForUserAsync_CouponFound_TierNotEligible_ReturnsFalse(
        User user,
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        // Arrange — discount exists and is active but user is not eligible for this audience type
        discount.AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions;
        discount.StartDate = DateTime.UtcNow.AddDays(-1);
        discount.EndDate = DateTime.UtcNow.AddDays(30);

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetActiveDiscountsAsync()
            .Returns([discount]);

        var filter = Substitute.For<IDiscountAudienceFilter>();
        filter.IsUserEligible(user, discount).Returns(DiscountDictionary(false));
        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .GetFilter(DiscountAudienceType.UserHasNoPreviousSubscriptions)
            .Returns(filter);

        // Act
        var result = await sutProvider.Sut.ValidateDiscountEligibilityForUserAsync(
            user, [discount.StripeCouponId], DiscountTierType.Families);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountEligibilityForUserAsync_InactiveDiscount_ReturnsFalse(
        User user,
        SubscriptionDiscount discount,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        // Arrange — expired discount is not returned by GetActiveDiscountsAsync, so won't appear in eligible set
        discount.StartDate = DateTime.UtcNow.AddDays(-30);
        discount.EndDate = DateTime.UtcNow.AddDays(-1);

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetActiveDiscountsAsync()
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.ValidateDiscountEligibilityForUserAsync(
            user, [discount.StripeCouponId], DiscountTierType.Premium);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountEligibilityForUserAsync_MultipleCoupons_AllEligible_ReturnsTrue(
        User user,
        SubscriptionDiscount discount1,
        SubscriptionDiscount discount2,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        // Arrange
        discount1.AudienceType = DiscountAudienceType.AllUsers;
        discount1.StartDate = DateTime.UtcNow.AddDays(-1);
        discount1.EndDate = DateTime.UtcNow.AddDays(30);
        discount2.AudienceType = DiscountAudienceType.AllUsers;
        discount2.StartDate = DateTime.UtcNow.AddDays(-1);
        discount2.EndDate = DateTime.UtcNow.AddDays(30);

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetActiveDiscountsAsync()
            .Returns([discount1, discount2]);

        var filter = Substitute.For<IDiscountAudienceFilter>();
        filter.IsUserEligible(user, discount1).Returns(DiscountDictionary(true));
        filter.IsUserEligible(user, discount2).Returns(DiscountDictionary(true));
        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .GetFilter(DiscountAudienceType.AllUsers)
            .Returns(filter);

        // Act
        var result = await sutProvider.Sut.ValidateDiscountEligibilityForUserAsync(
            user, [discount1.StripeCouponId, discount2.StripeCouponId], DiscountTierType.Premium);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateDiscountEligibilityForUserAsync_MultipleCoupons_OneNotEligible_ReturnsFalse(
        User user,
        SubscriptionDiscount discount1,
        SubscriptionDiscount discount2,
        SutProvider<SubscriptionDiscountService> sutProvider)
    {
        // Arrange — discount1 is eligible, discount2 is not
        discount1.AudienceType = DiscountAudienceType.AllUsers;
        discount1.StartDate = DateTime.UtcNow.AddDays(-1);
        discount1.EndDate = DateTime.UtcNow.AddDays(30);
        discount2.AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions;
        discount2.StartDate = DateTime.UtcNow.AddDays(-1);
        discount2.EndDate = DateTime.UtcNow.AddDays(30);

        sutProvider.GetDependency<ISubscriptionDiscountRepository>()
            .GetActiveDiscountsAsync()
            .Returns([discount1, discount2]);

        var allUsersFilter = Substitute.For<IDiscountAudienceFilter>();
        allUsersFilter.IsUserEligible(user, discount1).Returns(DiscountDictionary(true));
        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .GetFilter(DiscountAudienceType.AllUsers)
            .Returns(allUsersFilter);

        var restrictedFilter = Substitute.For<IDiscountAudienceFilter>();
        restrictedFilter.IsUserEligible(user, discount2).Returns(DiscountDictionary(false));
        sutProvider.GetDependency<IDiscountAudienceFilterFactory>()
            .GetFilter(DiscountAudienceType.UserHasNoPreviousSubscriptions)
            .Returns(restrictedFilter);

        // Act
        var result = await sutProvider.Sut.ValidateDiscountEligibilityForUserAsync(
            user, [discount1.StripeCouponId, discount2.StripeCouponId], DiscountTierType.Premium);

        // Assert
        Assert.False(result);
    }

}
