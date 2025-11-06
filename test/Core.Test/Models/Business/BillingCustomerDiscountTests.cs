using Bit.Core.Models.Business;
using Bit.Test.Common.AutoFixture.Attributes;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Models.Business;

public class BillingCustomerDiscountTests
{
    [Theory]
    [BitAutoData]
    public void Constructor_WithPercentageDiscount_SetsIdActivePercentOffAndAppliesTo(string couponId)
    {
        // Arrange
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = couponId,
                PercentOff = 25.5m,
                AmountOff = null,
                AppliesTo = new CouponAppliesTo
                {
                    Products = new List<string> { "product1", "product2" }
                }
            },
            End = null // Active discount
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Equal(couponId, result.Id);
        Assert.True(result.Active);
        Assert.Equal(25.5m, result.PercentOff);
        Assert.Null(result.AmountOff);
        Assert.NotNull(result.AppliesTo);
        Assert.Equal(2, result.AppliesTo.Count);
        Assert.Contains("product1", result.AppliesTo);
        Assert.Contains("product2", result.AppliesTo);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithAmountDiscount_ConvertsFromCentsToDollars(string couponId)
    {
        // Arrange - Stripe sends 1400 cents for $14.00
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = couponId,
                PercentOff = null,
                AmountOff = 1400, // 1400 cents
                AppliesTo = new CouponAppliesTo
                {
                    Products = new List<string>()
                }
            },
            End = null
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Equal(couponId, result.Id);
        Assert.True(result.Active);
        Assert.Null(result.PercentOff);
        Assert.Equal(14.00m, result.AmountOff); // Converted to dollars
        Assert.NotNull(result.AppliesTo);
        Assert.Empty(result.AppliesTo);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithInactiveDiscount_SetsActiveToFalse(string couponId)
    {
        // Arrange
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = couponId,
                PercentOff = 15m
            },
            End = DateTime.UtcNow.AddDays(-1) // Expired discount
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Equal(couponId, result.Id);
        Assert.False(result.Active);
        Assert.Equal(15m, result.PercentOff);
    }

    [Fact]
    public void Constructor_WithNullCoupon_SetsDiscountPropertiesToNull()
    {
        // Arrange
        var discount = new Discount
        {
            Coupon = null,
            End = null
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Null(result.Id);
        Assert.True(result.Active);
        Assert.Null(result.PercentOff);
        Assert.Null(result.AmountOff);
        Assert.Null(result.AppliesTo);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithNullAmountOff_SetsAmountOffToNull(string couponId)
    {
        // Arrange
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = couponId,
                PercentOff = 10m,
                AmountOff = null
            },
            End = null
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Null(result.AmountOff);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithZeroAmountOff_ConvertsCorrectly(string couponId)
    {
        // Arrange
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = couponId,
                AmountOff = 0
            },
            End = null
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Equal(0m, result.AmountOff);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithLargeAmountOff_ConvertsCorrectly(string couponId)
    {
        // Arrange - $100.00 discount
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = couponId,
                AmountOff = 10000 // 10000 cents = $100.00
            },
            End = null
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Equal(100.00m, result.AmountOff);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithSmallAmountOff_ConvertsCorrectly(string couponId)
    {
        // Arrange - $0.50 discount
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = couponId,
                AmountOff = 50 // 50 cents = $0.50
            },
            End = null
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Equal(0.50m, result.AmountOff);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithBothDiscountTypes_SetsPercentOffAndAmountOff(string couponId)
    {
        // Arrange - Coupon with both percentage and amount (edge case)
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = couponId,
                PercentOff = 20m,
                AmountOff = 500 // $5.00
            },
            End = null
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Equal(20m, result.PercentOff);
        Assert.Equal(5.00m, result.AmountOff);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithNullAppliesTo_SetsAppliesToNull(string couponId)
    {
        // Arrange
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = couponId,
                PercentOff = 10m,
                AppliesTo = null
            },
            End = null
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Null(result.AppliesTo);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithNullProductsList_SetsAppliesToNull(string couponId)
    {
        // Arrange
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = couponId,
                PercentOff = 10m,
                AppliesTo = new CouponAppliesTo
                {
                    Products = null
                }
            },
            End = null
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Null(result.AppliesTo);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithDecimalAmountOff_RoundsCorrectly(string couponId)
    {
        // Arrange - 1425 cents = $14.25
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = couponId,
                AmountOff = 1425
            },
            End = null
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Equal(14.25m, result.AmountOff);
    }

    [Fact]
    public void Constructor_DefaultConstructor_InitializesAllPropertiesToNullOrFalse()
    {
        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount();

        // Assert
        Assert.Null(result.Id);
        Assert.False(result.Active);
        Assert.Null(result.PercentOff);
        Assert.Null(result.AmountOff);
        Assert.Null(result.AppliesTo);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithFutureEndDate_SetsActiveToFalse(string couponId)
    {
        // Arrange - Discount expires in the future
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = couponId,
                PercentOff = 20m
            },
            End = DateTime.UtcNow.AddDays(30) // Expires in 30 days
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.False(result.Active); // Should be inactive because End is not null
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithPastEndDate_SetsActiveToFalse(string couponId)
    {
        // Arrange - Discount already expired
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = couponId,
                PercentOff = 20m
            },
            End = DateTime.UtcNow.AddDays(-30) // Expired 30 days ago
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.False(result.Active); // Should be inactive because End is not null
    }

    [Fact]
    public void Constructor_WithNullCouponId_SetsIdToNull()
    {
        // Arrange
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = null,
                PercentOff = 20m
            },
            End = null
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Null(result.Id);
        Assert.True(result.Active);
        Assert.Equal(20m, result.PercentOff);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithNullPercentOff_SetsPercentOffToNull(string couponId)
    {
        // Arrange
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = couponId,
                PercentOff = null,
                AmountOff = 1000
            },
            End = null
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.Null(result.PercentOff);
        Assert.Equal(10.00m, result.AmountOff);
    }

    [Fact]
    public void Constructor_WithCompleteStripeDiscount_MapsAllProperties()
    {
        // Arrange - Comprehensive test with all Stripe Discount properties set
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = "premium_discount_2024",
                PercentOff = 25m,
                AmountOff = 1500, // $15.00
                AppliesTo = new CouponAppliesTo
                {
                    Products = new List<string> { "prod_premium", "prod_family", "prod_teams" }
                }
            },
            End = null // Active
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert - Verify all properties mapped correctly
        Assert.Equal("premium_discount_2024", result.Id);
        Assert.True(result.Active);
        Assert.Equal(25m, result.PercentOff);
        Assert.Equal(15.00m, result.AmountOff);
        Assert.NotNull(result.AppliesTo);
        Assert.Equal(3, result.AppliesTo.Count);
        Assert.Contains("prod_premium", result.AppliesTo);
        Assert.Contains("prod_family", result.AppliesTo);
        Assert.Contains("prod_teams", result.AppliesTo);
    }

    [Fact]
    public void Constructor_WithMinimalStripeDiscount_HandlesNullsGracefully()
    {
        // Arrange - Minimal Stripe Discount with most properties null
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = null,
                PercentOff = null,
                AmountOff = null,
                AppliesTo = null
            },
            End = DateTime.UtcNow.AddDays(10) // Has end date
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert - Should handle all nulls gracefully
        Assert.Null(result.Id);
        Assert.False(result.Active);
        Assert.Null(result.PercentOff);
        Assert.Null(result.AmountOff);
        Assert.Null(result.AppliesTo);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithEmptyProductsList_PreservesEmptyList(string couponId)
    {
        // Arrange
        var discount = new Discount
        {
            Coupon = new Coupon
            {
                Id = couponId,
                PercentOff = 10m,
                AppliesTo = new CouponAppliesTo
                {
                    Products = new List<string>() // Empty but not null
                }
            },
            End = null
        };

        // Act
        var result = new SubscriptionInfo.BillingCustomerDiscount(discount);

        // Assert
        Assert.NotNull(result.AppliesTo);
        Assert.Empty(result.AppliesTo);
    }
}
