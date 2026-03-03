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
    public async Task Run_NoEligibleDiscounts_ReturnsEmptyArray(
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
        Assert.Empty(result.AsT0);
    }

    [Theory, BitAutoData]
    public async Task Run_EligibleDiscounts_ReturnsMappedResponseModels(
        User user,
        SubscriptionDiscount discount,
        SutProvider<GetApplicableDiscountsQuery> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .GetEligibleDiscountsAsync(user)
            .Returns(new List<DiscountEligibility> { new(discount, DiscountDictionary(true)) });

        // Act
        var result = await sutProvider.Sut.Run(user);

        // Assert
        Assert.True(result.IsT0);
        var models = result.AsT0;
        var model = Assert.Single(models);
        Assert.Equal(discount.StripeCouponId, model.StripeCouponId);
        Assert.Equal(discount.StripeProductIds, model.StripeProductIds);
        Assert.Equal(discount.PercentOff, model.PercentOff);
        Assert.Equal(discount.AmountOff, model.AmountOff);
        Assert.Equal(discount.Duration, model.Duration);
        Assert.Equal(discount.Name, model.Name);
        Assert.Equal(discount.StartDate, model.StartDate);
        Assert.Equal(discount.EndDate, model.EndDate);
    }

    [Theory, BitAutoData]
    public async Task Run_DiscountWithProductIds_MapsAllProductIds(
        User user,
        SubscriptionDiscount discount,
        SutProvider<GetApplicableDiscountsQuery> sutProvider)
    {
        // Arrange
        var productIds = new List<string> { "prod_123", "prod_456", "prod_789" };
        discount.StripeProductIds = productIds;

        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .GetEligibleDiscountsAsync(user)
            .Returns(new List<DiscountEligibility> { new(discount, DiscountDictionary(true)) });

        // Act
        var result = await sutProvider.Sut.Run(user);

        // Assert
        var model = Assert.Single(result.AsT0);
        Assert.NotNull(model.StripeProductIds);
        Assert.Equal(productIds, model.StripeProductIds);
    }

    [Theory, BitAutoData]
    public async Task Run_DiscountWithNoProductIds_MapsNullProductIds(
        User user,
        SubscriptionDiscount discount,
        SutProvider<GetApplicableDiscountsQuery> sutProvider)
    {
        // Arrange
        discount.StripeProductIds = null;

        sutProvider.GetDependency<ISubscriptionDiscountService>()
            .GetEligibleDiscountsAsync(user)
            .Returns(new List<DiscountEligibility> { new(discount, DiscountDictionary(false)) });

        // Act
        var result = await sutProvider.Sut.Run(user);

        // Assert
        var model = Assert.Single(result.AsT0);
        Assert.Null(model.StripeProductIds);
    }

    [Theory, BitAutoData]
    public async Task Run_MultipleEligibleDiscounts_ReturnsAllMappedResponseModels(
        User user,
        SubscriptionDiscount firstDiscount,
        SubscriptionDiscount secondDiscount,
        SutProvider<GetApplicableDiscountsQuery> sutProvider)
    {
        // Arrange
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
        var models = result.AsT0;
        Assert.Equal(2, models.Length);
        Assert.Contains(models, m => m.StripeCouponId == firstDiscount.StripeCouponId);
        Assert.Contains(models, m => m.StripeCouponId == secondDiscount.StripeCouponId);
    }
}
