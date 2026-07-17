using Bit.Core.Models.Business;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Models.Business;

public class SubscriptionInfoTests
{
    [Fact]
    public void BillingSubscriptionItem_NullPlan_HandlesGracefully()
    {
        // Arrange - SubscriptionItem with null Plan
        var subscriptionItem = new SubscriptionItem
        {
            Plan = null,
            Quantity = 1
        };

        // Act
        var result = new SubscriptionInfo.BillingSubscription.BillingSubscriptionItem(subscriptionItem);

        // Assert - Should handle null Plan gracefully
        Assert.Null(result.ProductId);
        Assert.Null(result.Name);
        Assert.Equal(0m, result.Amount); // Defaults to 0 when Plan is null
        Assert.Null(result.Interval);
        Assert.Equal(1, result.Quantity);
        Assert.False(result.SponsoredSubscriptionItem);
        Assert.False(result.AddonSubscriptionItem);
    }

    [Fact]
    public void BillingSubscriptionItem_AddonFlag_DerivedFromPriceMetadata()
    {
        // Arrange - the add-on flag lives on the price's metadata (production shape)
        var subscriptionItem = new SubscriptionItem
        {
            Price = new Price { Id = "storage-gb-monthly", Metadata = new Dictionary<string, string> { { "isAddOn", "true" } } },
            Plan = new Plan { Id = "storage-gb-monthly", ProductId = "prod_storage", Nickname = "Additional Storage GB", Amount = 50, Interval = "month" },
            Quantity = 3
        };

        // Act
        var result = new SubscriptionInfo.BillingSubscription.BillingSubscriptionItem(subscriptionItem);

        // Assert
        Assert.True(result.AddonSubscriptionItem);
    }

    [Fact]
    public void BillingSubscriptionItem_AddonFlag_IgnoresItemMetadata()
    {
        // Arrange - isAddOn on the subscription item's own metadata must be ignored (never set in production)
        var subscriptionItem = new SubscriptionItem
        {
            Price = new Price { Id = "teams-seat", Metadata = new Dictionary<string, string>() },
            Plan = new Plan { Id = "teams-seat", ProductId = "prod_teams", Nickname = "Teams Organization Seat", Amount = 400, Interval = "month" },
            Quantity = 5,
            Metadata = new Dictionary<string, string> { { "isAddOn", "true" } }
        };

        // Act
        var result = new SubscriptionInfo.BillingSubscription.BillingSubscriptionItem(subscriptionItem);

        // Assert
        Assert.False(result.AddonSubscriptionItem);
    }

    [Fact]
    public void BillingSubscriptionItem_NullAmount_SetsToZero()
    {
        // Arrange - SubscriptionItem with Plan but null Amount
        var subscriptionItem = new SubscriptionItem
        {
            Plan = new Plan
            {
                ProductId = "prod_test",
                Nickname = "Test Plan",
                Amount = null, // Null amount
                Interval = "month"
            },
            Quantity = 1
        };

        // Act
        var result = new SubscriptionInfo.BillingSubscription.BillingSubscriptionItem(subscriptionItem);

        // Assert - Should default to 0 when Amount is null
        Assert.Equal("prod_test", result.ProductId);
        Assert.Equal("Test Plan", result.Name);
        Assert.Equal(0m, result.Amount); // Business rule: defaults to 0 when null
        Assert.Equal("month", result.Interval);
        Assert.Equal(1, result.Quantity);
    }

    [Fact]
    public void BillingSubscriptionItem_ZeroAmount_PreservesZero()
    {
        // Arrange - SubscriptionItem with Plan and zero Amount
        var subscriptionItem = new SubscriptionItem
        {
            Plan = new Plan
            {
                ProductId = "prod_test",
                Nickname = "Test Plan",
                Amount = 0, // Zero amount (0 cents)
                Interval = "month"
            },
            Quantity = 1
        };

        // Act
        var result = new SubscriptionInfo.BillingSubscription.BillingSubscriptionItem(subscriptionItem);

        // Assert - Should preserve zero amount
        Assert.Equal("prod_test", result.ProductId);
        Assert.Equal("Test Plan", result.Name);
        Assert.Equal(0m, result.Amount); // Zero amount preserved
        Assert.Equal("month", result.Interval);
    }

    [Fact]
    public void BillingUpcomingInvoice_ZeroAmountDue_ConvertsToZero()
    {
        // Arrange - Invoice with zero AmountDue
        // Note: Stripe's Invoice.AmountDue is non-nullable long, so we test with 0
        // The null-coalescing operator (?? 0) in the constructor handles the case where
        // ConvertFromStripeMinorUnits returns null, but since AmountDue is non-nullable,
        // this test verifies the conversion path works correctly for zero values
        var invoice = new Invoice
        {
            AmountDue = 0, // Zero amount due (0 cents)
            Created = DateTime.UtcNow
        };

        // Act
        var result = new SubscriptionInfo.BillingUpcomingInvoice(invoice);

        // Assert - Should convert zero correctly
        Assert.Equal(0m, result.Amount);
        Assert.NotNull(result.Date);
    }

    [Fact]
    public void BillingUpcomingInvoice_ValidAmountDue_ConvertsCorrectly()
    {
        // Arrange - Invoice with valid AmountDue
        var invoice = new Invoice
        {
            AmountDue = 2500, // 2500 cents = $25.00
            Created = DateTime.UtcNow
        };

        // Act
        var result = new SubscriptionInfo.BillingUpcomingInvoice(invoice);

        // Assert - Should convert correctly
        Assert.Equal(25.00m, result.Amount); // Converted from cents
        Assert.NotNull(result.Date);
    }

    [Fact]
    public void BillingCustomerDiscount_DiscountCtor_WithEndDate_SetsEndAndDurationAndActiveFalse()
    {
        // Arrange - repeating discount with an absolute end date
        var end = DateTime.UtcNow.AddMonths(12);
        var discount = new Discount
        {
            End = end,
            Coupon = new Coupon
            {
                PercentOff = 10m,
                Duration = "repeating",
                DurationInMonths = 12
            }
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Equal(end, result.End);
        Assert.Equal(12, result.DurationInMonths);
        Assert.False(result.Active);   // End != null => not perpetual (UNCHANGED semantics)
        Assert.Equal(10m, result.PercentOff);
    }

    [Fact]
    public void BillingCustomerDiscount_DiscountCtor_NoEndDate_SetsEndNullAndActiveTrue()
    {
        // Arrange - perpetual discount (no end date)
        var discount = new Discount
        {
            End = null,
            Coupon = new Coupon
            {
                PercentOff = 10m,
                Duration = "forever"
            }
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Null(result.End);
        Assert.True(result.Active);   // End == null => perpetual (UNCHANGED semantics)
    }

    [Fact]
    public void BillingCustomerDiscount_CouponCtor_SetsEndNullDurationAndActiveTrue()
    {
        // Arrange - Phase-2 scheduled discount: a Coupon with no Discount wrapper
        var coupon = new Coupon
        {
            PercentOff = 10m,
            Duration = "repeating",
            DurationInMonths = 12
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(coupon);

        // Assert
        Assert.Null(result.End);            // no Discount wrapper => no end date
        Assert.Equal(12, result.DurationInMonths);
        Assert.True(result.Active);         // Coupon ctor: Active unconditionally true (UNCHANGED)
    }

    [Fact]
    public void BillingCustomerDiscount_DiscountCtor_AmountOffRepeatingWithEnd_AmountEndAndDurationCoexist()
    {
        // Arrange - amount-off repeating discount with an end date
        var end = DateTime.UtcNow.AddMonths(6);
        var discount = new Discount
        {
            End = end,
            Coupon = new Coupon
            {
                AmountOff = 1500, // $15.00
                PercentOff = null,
                Duration = "repeating",
                DurationInMonths = 6
            }
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert - amount (in dollars), end, and duration all present on one discount
        Assert.Equal(15.00m, result.AmountOff);
        Assert.Null(result.PercentOff);
        Assert.Equal(end, result.End);
        Assert.Equal(6, result.DurationInMonths);
        Assert.False(result.Active);
    }

    [Theory]
    [InlineData("unpaid", false)] // Recoverable, not terminal — regression guard for PM-40015
    [InlineData("canceled", true)]
    [InlineData("incomplete_expired", true)]
    [InlineData("past_due", false)]
    [InlineData("active", false)]
    [InlineData("trialing", false)]
    [InlineData("incomplete", false)]
    [InlineData("paused", false)]
    [InlineData(null, false)]
    public void BillingSubscription_Cancelled_DerivedFromStatus(string? status, bool expectedCancelled)
    {
        // Act
        var result = new SubscriptionInfo.BillingSubscription(new Subscription { Status = status });

        // Assert
        Assert.Equal(status, result.Status);
        Assert.Equal(expectedCancelled, result.Cancelled);
    }
}

