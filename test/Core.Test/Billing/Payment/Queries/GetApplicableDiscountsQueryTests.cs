using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Payment.Queries;

[SutProviderCustomize]
public class GetApplicableDiscountsQueryTests
{
    private static IDictionary<DiscountTierType, bool> DiscountDictionary(bool eligibilitySetting)
        => Enum.GetValues<DiscountTierType>().ToDictionary(t => t, _ => eligibilitySetting);

    [Theory, BitAutoData]
    public async Task Run_NoEligibleDiscounts_ReturnsBothEmpty(
        User user,
        SutProvider<GetApplicableDiscountsQuery> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .GetEligibleDiscountsAsync(user)
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.Run(user);

        // Assert
        Assert.True(result.IsT0);
        Assert.Empty(result.AsT0.CartLevelDiscounts);
        Assert.Empty(result.AsT0.ItemLevelDiscounts);
    }

    [Theory, BitAutoData]
    public async Task Run_CartLevelDiscount_NullStripeProductIds_AppearsInCartLevel(
        User user,
        SubscriptionDiscount discount,
        SutProvider<GetApplicableDiscountsQuery> sutProvider)
    {
        // Arrange
        discount.StripeProductIds = null;

        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .GetEligibleDiscountsAsync(user)
            .Returns(new List<DiscountEligibility> { new(discount, DiscountDictionary(true)) });

        // Act
        var result = await sutProvider.Sut.Run(user);

        // Assert
        Assert.True(result.IsT0);
        var model = Assert.Single(result.AsT0.CartLevelDiscounts);
        Assert.Empty(result.AsT0.ItemLevelDiscounts);
        Assert.Equal(discount.StripeCouponId, model.StripeCouponId);
    }

    [Theory, BitAutoData]
    public async Task Run_CartLevelDiscount_EmptyStripeProductIds_AppearsInCartLevel(
        User user,
        SubscriptionDiscount discount,
        SutProvider<GetApplicableDiscountsQuery> sutProvider)
    {
        // Arrange
        discount.StripeProductIds = [];

        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .GetEligibleDiscountsAsync(user)
            .Returns(new List<DiscountEligibility> { new(discount, DiscountDictionary(true)) });

        // Act
        var result = await sutProvider.Sut.Run(user);

        // Assert
        Assert.True(result.IsT0);
        var model = Assert.Single(result.AsT0.CartLevelDiscounts);
        Assert.Empty(result.AsT0.ItemLevelDiscounts);
        Assert.Equal(discount.StripeCouponId, model.StripeCouponId);
    }

    [Theory, BitAutoData]
    public async Task Run_ItemLevelDiscount_WithStripeProductIds_AppearsInItemLevel(
        User user,
        SubscriptionDiscount discount,
        SutProvider<GetApplicableDiscountsQuery> sutProvider)
    {
        // Arrange
        discount.StripeProductIds = ["prod_premium_seat"];

        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .GetEligibleDiscountsAsync(user)
            .Returns(new List<DiscountEligibility> { new(discount, DiscountDictionary(true)) });

        // Act
        var result = await sutProvider.Sut.Run(user);

        // Assert
        Assert.True(result.IsT0);
        Assert.Empty(result.AsT0.CartLevelDiscounts);
        var model = Assert.Single(result.AsT0.ItemLevelDiscounts);
        Assert.Equal(discount.StripeCouponId, model.StripeCouponId);
    }

    [Theory, BitAutoData]
    public async Task Run_MixedDiscounts_SplitsCorrectly(
        User user,
        SubscriptionDiscount cartDiscount,
        SubscriptionDiscount itemDiscount,
        SutProvider<GetApplicableDiscountsQuery> sutProvider)
    {
        // Arrange
        cartDiscount.StripeProductIds = null;
        itemDiscount.StripeProductIds = ["prod_premium_seat"];

        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .GetEligibleDiscountsAsync(user)
            .Returns(new List<DiscountEligibility>
            {
                new(cartDiscount, DiscountDictionary(true)),
                new(itemDiscount, DiscountDictionary(true))
            });

        // Act
        var result = await sutProvider.Sut.Run(user);

        // Assert
        Assert.True(result.IsT0);
        var cartModel = Assert.Single(result.AsT0.CartLevelDiscounts);
        var itemModel = Assert.Single(result.AsT0.ItemLevelDiscounts);
        Assert.Equal(cartDiscount.StripeCouponId, cartModel.StripeCouponId);
        Assert.Equal(itemDiscount.StripeCouponId, itemModel.StripeCouponId);
    }

    [Theory, BitAutoData]
    public async Task Run_EligibleDiscounts_MapsAllFieldsCorrectly(
        User user,
        SubscriptionDiscount discount,
        SutProvider<GetApplicableDiscountsQuery> sutProvider)
    {
        // Arrange
        discount.StripeProductIds = null;

        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .GetEligibleDiscountsAsync(user)
            .Returns(new List<DiscountEligibility> { new(discount, DiscountDictionary(true)) });

        // Act
        var result = await sutProvider.Sut.Run(user);

        // Assert
        Assert.True(result.IsT0);
        var model = Assert.Single(result.AsT0.CartLevelDiscounts);
        Assert.Equal(discount.StripeCouponId, model.StripeCouponId);
        Assert.Equal(discount.PercentOff, model.PercentOff);
        Assert.Equal(discount.AmountOff, model.AmountOff);
        Assert.Equal(discount.Duration, model.Duration);
        Assert.Equal(discount.Name, model.Name);
        Assert.Equal(discount.StartDate, model.StartDate);
        Assert.Equal(discount.EndDate, model.EndDate);
        Assert.All(model.TierEligibility!.Values, Assert.True);
    }

    [Theory, BitAutoData]
    public async Task Run_DiscountWithAllTiersEligible_MapsAllTierEligibilityTrue(
        User user,
        SubscriptionDiscount discount,
        SutProvider<GetApplicableDiscountsQuery> sutProvider)
    {
        // Arrange
        discount.StripeProductIds = null;
        var tierEligibility = DiscountDictionary(true);

        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .GetEligibleDiscountsAsync(user)
            .Returns(new List<DiscountEligibility> { new(discount, tierEligibility) });

        // Act
        var result = await sutProvider.Sut.Run(user);

        // Assert
        var model = Assert.Single(result.AsT0.CartLevelDiscounts);
        Assert.NotNull(model.TierEligibility);
        Assert.All(model.TierEligibility.Values, Assert.True);
    }

    [Theory, BitAutoData]
    public async Task Run_DiscountWithPartialTierEligibility_MapsSpecificTierEligibility(
        User user,
        SubscriptionDiscount discount,
        SutProvider<GetApplicableDiscountsQuery> sutProvider)
    {
        // Arrange
        discount.StripeProductIds = null;
        var tierEligibility = new Dictionary<DiscountTierType, bool>
        {
            { DiscountTierType.Premium, true },
            { DiscountTierType.Families, false }
        };

        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .GetEligibleDiscountsAsync(user)
            .Returns(new List<DiscountEligibility> { new(discount, tierEligibility) });

        // Act
        var result = await sutProvider.Sut.Run(user);

        // Assert
        var model = Assert.Single(result.AsT0.CartLevelDiscounts);
        Assert.NotNull(model.TierEligibility);
        Assert.True(model.TierEligibility[DiscountTierType.Premium]);
        Assert.False(model.TierEligibility[DiscountTierType.Families]);
    }

    [Theory, BitAutoData]
    public async Task Run_MultipleCartLevelDiscounts_ReturnsAllInCartLevel(
        User user,
        SubscriptionDiscount firstDiscount,
        SubscriptionDiscount secondDiscount,
        SutProvider<GetApplicableDiscountsQuery> sutProvider)
    {
        // Arrange
        firstDiscount.StripeProductIds = null;
        secondDiscount.StripeProductIds = [];

        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .GetEligibleDiscountsAsync(user)
            .Returns(new List<DiscountEligibility>
            {
                new(firstDiscount, DiscountDictionary(true)),
                new(secondDiscount, DiscountDictionary(true))
            });

        // Act
        var result = await sutProvider.Sut.Run(user);

        // Assert
        Assert.True(result.IsT0);
        Assert.Equal(2, result.AsT0.CartLevelDiscounts.Length);
        Assert.Contains(result.AsT0.CartLevelDiscounts, m => m.StripeCouponId == firstDiscount.StripeCouponId);
        Assert.Contains(result.AsT0.CartLevelDiscounts, m => m.StripeCouponId == secondDiscount.StripeCouponId);
        Assert.Empty(result.AsT0.ItemLevelDiscounts);
    }
}
