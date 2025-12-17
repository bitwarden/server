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
}

