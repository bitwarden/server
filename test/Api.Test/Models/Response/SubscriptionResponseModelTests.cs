using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Models.Response;

public class SubscriptionResponseModelTests
{
    [Theory]
    [BitAutoData]
    public void Constructor_WithIncludeDiscountTrue_AndMatchingCouponId_ReturnsDiscount(
        User user,
        UserLicense license)
    {
        // Arrange
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = Constants.PremiumDiscountCouponId, // Matching coupon ID
                Active = true,
                PercentOff = 20m,
                AmountOff = null,
                AppliesTo = new List<string> { "product1" }
            }
        };

        // Act
        var result = new SubscriptionResponseModel(user, subscriptionInfo, license, includeDiscount: true);

        // Assert
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(Constants.PremiumDiscountCouponId, result.CustomerDiscount.Id);
        Assert.True(result.CustomerDiscount.Active);
        Assert.Equal(20m, result.CustomerDiscount.PercentOff);
        Assert.Null(result.CustomerDiscount.AmountOff);
        Assert.Single(result.CustomerDiscount.AppliesTo);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithIncludeDiscountTrue_AndNonMatchingCouponId_ReturnsNull(
        User user,
        UserLicense license)
    {
        // Arrange
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = "different-coupon-id", // Non-matching coupon ID
                Active = true,
                PercentOff = 20m,
                AmountOff = null,
                AppliesTo = new List<string> { "product1" }
            }
        };

        // Act
        var result = new SubscriptionResponseModel(user, subscriptionInfo, license, includeDiscount: true);

        // Assert
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithIncludeDiscountFalse_AndMatchingCouponId_ReturnsNull(
        User user,
        UserLicense license)
    {
        // Arrange
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = Constants.PremiumDiscountCouponId, // Matching coupon ID
                Active = true,
                PercentOff = 20m,
                AmountOff = null,
                AppliesTo = new List<string> { "product1" }
            }
        };

        // Act
        var result = new SubscriptionResponseModel(user, subscriptionInfo, license, includeDiscount: false);

        // Assert - Should be null because includeDiscount is false
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithNullCustomerDiscount_ReturnsNull(
        User user,
        UserLicense license)
    {
        // Arrange
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = null
        };

        // Act
        var result = new SubscriptionResponseModel(user, subscriptionInfo, license, includeDiscount: true);

        // Assert
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithAmountOffDiscount_AndMatchingCouponId_ReturnsDiscount(
        User user,
        UserLicense license)
    {
        // Arrange
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = Constants.PremiumDiscountCouponId,
                Active = true,
                PercentOff = null,
                AmountOff = 14.00m, // Already converted from cents in BillingCustomerDiscount
                AppliesTo = new List<string>()
            }
        };

        // Act
        var result = new SubscriptionResponseModel(user, subscriptionInfo, license, includeDiscount: true);

        // Assert
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(Constants.PremiumDiscountCouponId, result.CustomerDiscount.Id);
        Assert.Null(result.CustomerDiscount.PercentOff);
        Assert.Equal(14.00m, result.CustomerDiscount.AmountOff);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_DefaultIncludeDiscount_ReturnsNull(
        User user,
        UserLicense license)
    {
        // Arrange
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = Constants.PremiumDiscountCouponId,
                Active = true,
                PercentOff = 20m
            }
        };

        // Act - Using default parameter (includeDiscount defaults to false)
        var result = new SubscriptionResponseModel(user, subscriptionInfo, license);

        // Assert
        Assert.Null(result.CustomerDiscount);
    }
}
