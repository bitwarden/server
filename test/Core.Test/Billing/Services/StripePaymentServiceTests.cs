using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class StripePaymentServiceTests
{
    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithCustomerDiscount_ReturnsDiscountFromCustomer(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var customerDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount,
                PercentOff = 20m,
                AmountOff = 1400
            },
            End = null
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer
            {
                Discount = customerDiscount
            },
            Discounts = new List<Discount>(), // Empty list
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(
                subscriber.GatewaySubscriptionId,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(StripeConstants.CouponIDs.Milestone2SubscriptionDiscount, result.CustomerDiscount.Id);
        Assert.Equal(20m, result.CustomerDiscount.PercentOff);
        Assert.Equal(14.00m, result.CustomerDiscount.AmountOff); // Converted from cents
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithoutCustomerDiscount_FallsBackToSubscriptionDiscounts(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscriptionDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount,
                PercentOff = 15m,
                AmountOff = null
            },
            End = null
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer
            {
                Discount = null // No customer discount
            },
            Discounts = new List<Discount> { subscriptionDiscount },
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(
                subscriber.GatewaySubscriptionId,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert - Should use subscription discount as fallback
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(StripeConstants.CouponIDs.Milestone2SubscriptionDiscount, result.CustomerDiscount.Id);
        Assert.Equal(15m, result.CustomerDiscount.PercentOff);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithBothDiscounts_PrefersCustomerDiscount(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var customerDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount,
                PercentOff = 25m
            },
            End = null
        };

        var subscriptionDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = "different-coupon-id",
                PercentOff = 10m
            },
            End = null
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer
            {
                Discount = customerDiscount // Should prefer this
            },
            Discounts = new List<Discount> { subscriptionDiscount },
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(
                subscriber.GatewaySubscriptionId,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert - Should prefer customer discount over subscription discount
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(StripeConstants.CouponIDs.Milestone2SubscriptionDiscount, result.CustomerDiscount.Id);
        Assert.Equal(25m, result.CustomerDiscount.PercentOff);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithNoDiscounts_ReturnsNullDiscount(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer
            {
                Discount = null
            },
            Discounts = new List<Discount>(), // Empty list, no discounts
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(
                subscriber.GatewaySubscriptionId,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithMultipleSubscriptionDiscounts_SelectsFirstDiscount(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange - Multiple subscription-level discounts, no customer discount
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var firstDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = "coupon-10-percent",
                PercentOff = 10m
            },
            End = null
        };

        var secondDiscount = new Discount
        {
            Coupon = new Coupon
            {
                Id = "coupon-20-percent",
                PercentOff = 20m
            },
            End = null
        };

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer
            {
                Discount = null // No customer discount
            },
            // Multiple subscription discounts - FirstOrDefault() should select the first one
            Discounts = new List<Discount> { firstDiscount, secondDiscount },
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(
                subscriber.GatewaySubscriptionId,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert - Should select the first discount from the list (FirstOrDefault() behavior)
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal("coupon-10-percent", result.CustomerDiscount.Id);
        Assert.Equal(10m, result.CustomerDiscount.PercentOff);
        // Verify the second discount was not selected
        Assert.NotEqual("coupon-20-percent", result.CustomerDiscount.Id);
        Assert.NotEqual(20m, result.CustomerDiscount.PercentOff);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithNullCustomer_HandlesGracefully(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange - Subscription with null Customer (defensive null check scenario)
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = null, // Customer not expanded or null
            Discounts = new List<Discount>(), // Empty discounts
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(
                subscriber.GatewaySubscriptionId,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert - Should handle null Customer gracefully without throwing NullReferenceException
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithNullDiscounts_HandlesGracefully(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange - Subscription with null Discounts (defensive null check scenario)
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer
            {
                Discount = null // No customer discount
            },
            Discounts = null, // Discounts not expanded or null
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(
                subscriber.GatewaySubscriptionId,
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert - Should handle null Discounts gracefully without throwing NullReferenceException
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_VerifiesCorrectExpandOptions(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.Gateway = GatewayType.Stripe;
        subscriber.GatewayCustomerId = "cus_test123";
        subscriber.GatewaySubscriptionId = "sub_test123";

        var subscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically",
            Customer = new Customer { Discount = null },
            Discounts = new List<Discount>(), // Empty list
            Items = new StripeList<SubscriptionItem> { Data = [] }
        };

        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        stripeAdapter
            .GetSubscriptionAsync(
                Arg.Any<string>(),
                Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        // Act
        await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert - Verify expand options are correct
        await stripeAdapter.Received(1).GetSubscriptionAsync(
            subscriber.GatewaySubscriptionId,
            Arg.Is<SubscriptionGetOptions>(o =>
                o.Expand.Contains("customer.discount.coupon.applies_to") &&
                o.Expand.Contains("discounts.coupon.applies_to") &&
                o.Expand.Contains("test_clock")));
    }

    [Theory]
    [BitAutoData]
    public async Task GetSubscriptionAsync_WithEmptyGatewaySubscriptionId_ReturnsEmptySubscriptionInfo(
        SutProvider<StripePaymentService> sutProvider,
        User subscriber)
    {
        // Arrange
        subscriber.GatewaySubscriptionId = null;

        // Act
        var result = await sutProvider.Sut.GetSubscriptionAsync(subscriber);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Subscription);
        Assert.Null(result.CustomerDiscount);
        Assert.Null(result.UpcomingInvoice);

        // Verify no Stripe API calls were made
        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceive()
            .GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionGetOptions>());
    }
}
