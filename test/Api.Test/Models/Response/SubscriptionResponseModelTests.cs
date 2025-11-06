using Bit.Api.Models.Response;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Test.Common.AutoFixture.Attributes;
using Stripe;
using Xunit;

namespace Bit.Api.Test.Models.Response;

public class SubscriptionResponseModelTests
{
    [Theory]
    [BitAutoData]
    public void Constructor_IncludeDiscountTrueMatchingCouponId_ReturnsDiscount(
        User user,
        UserLicense license)
    {
        // Arrange
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount, // Matching coupon ID
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
        Assert.Equal(StripeConstants.CouponIDs.Milestone2SubscriptionDiscount, result.CustomerDiscount.Id);
        Assert.True(result.CustomerDiscount.Active);
        Assert.Equal(20m, result.CustomerDiscount.PercentOff);
        Assert.Null(result.CustomerDiscount.AmountOff);
        Assert.NotNull(result.CustomerDiscount.AppliesTo);
        Assert.Single(result.CustomerDiscount.AppliesTo);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_IncludeDiscountTrueNonMatchingCouponId_ReturnsNull(
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
    public void Constructor_IncludeDiscountFalseMatchingCouponId_ReturnsNull(
        User user,
        UserLicense license)
    {
        // Arrange
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount, // Matching coupon ID
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
    public void Constructor_NullCustomerDiscount_ReturnsNull(
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
    public void Constructor_AmountOffDiscountMatchingCouponId_ReturnsDiscount(
        User user,
        UserLicense license)
    {
        // Arrange
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount,
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
        Assert.Equal(StripeConstants.CouponIDs.Milestone2SubscriptionDiscount, result.CustomerDiscount.Id);
        Assert.Null(result.CustomerDiscount.PercentOff);
        Assert.Equal(14.00m, result.CustomerDiscount.AmountOff);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_DefaultIncludeDiscountParameter_ReturnsNull(
        User user,
        UserLicense license)
    {
        // Arrange
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount,
                Active = true,
                PercentOff = 20m
            }
        };

        // Act - Using default parameter (includeDiscount defaults to false)
        var result = new SubscriptionResponseModel(user, subscriptionInfo, license);

        // Assert
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_NullDiscountIdIncludeDiscountTrue_ReturnsNull(
        User user,
        UserLicense license)
    {
        // Arrange
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = null, // Null discount ID
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
    public void Constructor_MatchingCouponIdInactiveDiscount_ReturnsNull(
        User user,
        UserLicense license)
    {
        // Arrange
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount, // Matching coupon ID
                Active = false, // Inactive discount
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
    public void Constructor_UserOnly_SetsBasicProperties(User user)
    {
        // Arrange
        user.Storage = 5368709120; // 5 GB in bytes
        user.MaxStorageGb = (short)10;
        user.PremiumExpirationDate = DateTime.UtcNow.AddMonths(12);

        // Act
        var result = new SubscriptionResponseModel(user);

        // Assert
        Assert.NotNull(result.StorageName);
        Assert.Equal(5.0, result.StorageGb);
        Assert.Equal((short)10, result.MaxStorageGb);
        Assert.Equal(user.PremiumExpirationDate, result.Expiration);
        Assert.Null(result.License);
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_UserAndLicense_IncludesLicense(User user, UserLicense license)
    {
        // Arrange
        user.Storage = 1073741824; // 1 GB in bytes
        user.MaxStorageGb = (short)5;

        // Act
        var result = new SubscriptionResponseModel(user, license);

        // Assert
        Assert.NotNull(result.License);
        Assert.Equal(license, result.License);
        Assert.Equal(1.0, result.StorageGb);
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_NullStorage_SetsStorageToZero(User user)
    {
        // Arrange
        user.Storage = null;

        // Act
        var result = new SubscriptionResponseModel(user);

        // Assert
        Assert.Null(result.StorageName);
        Assert.Equal(0, result.StorageGb);
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_NullLicense_ExcludesLicense(User user)
    {
        // Act
        var result = new SubscriptionResponseModel(user, null);

        // Assert
        Assert.Null(result.License);
        Assert.Null(result.CustomerDiscount);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_BothPercentOffAndAmountOffPresent_HandlesEdgeCase(
        User user,
        UserLicense license)
    {
        // Arrange - Edge case: Both PercentOff and AmountOff present
        // This tests the scenario where Stripe coupon has both discount types
        var subscriptionInfo = new SubscriptionInfo
        {
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount,
                Active = true,
                PercentOff = 25m,
                AmountOff = 20.00m, // Already converted from cents
                AppliesTo = new List<string> { "prod_premium" }
            }
        };

        // Act
        var result = new SubscriptionResponseModel(user, subscriptionInfo, license, includeDiscount: true);

        // Assert - Both values should be preserved
        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(StripeConstants.CouponIDs.Milestone2SubscriptionDiscount, result.CustomerDiscount.Id);
        Assert.Equal(25m, result.CustomerDiscount.PercentOff);
        Assert.Equal(20.00m, result.CustomerDiscount.AmountOff);
        Assert.NotNull(result.CustomerDiscount.AppliesTo);
        Assert.Single(result.CustomerDiscount.AppliesTo);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithSubscriptionAndInvoice_MapsAllProperties(
        User user,
        UserLicense license)
    {
        // Arrange - Test with Subscription, UpcomingInvoice, and CustomerDiscount
        var stripeSubscription = new Subscription
        {
            Id = "sub_test123",
            Status = "active",
            CollectionMethod = "charge_automatically"
        };

        var stripeInvoice = new Invoice
        {
            AmountDue = 1500, // 1500 cents = $15.00
            Created = DateTime.UtcNow.AddDays(7)
        };

        var subscriptionInfo = new SubscriptionInfo
        {
            Subscription = new SubscriptionInfo.BillingSubscription(stripeSubscription),
            UpcomingInvoice = new SubscriptionInfo.BillingUpcomingInvoice(stripeInvoice),
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount,
                Active = true,
                PercentOff = 20m,
                AmountOff = null,
                AppliesTo = new List<string> { "prod_premium" }
            }
        };

        // Act
        var result = new SubscriptionResponseModel(user, subscriptionInfo, license, includeDiscount: true);

        // Assert - Verify all properties are mapped correctly
        Assert.NotNull(result.Subscription);
        Assert.Equal("active", result.Subscription.Status);
        Assert.Equal(14, result.Subscription.GracePeriod); // charge_automatically = 14 days

        Assert.NotNull(result.UpcomingInvoice);
        Assert.Equal(15.00m, result.UpcomingInvoice.Amount);
        Assert.NotNull(result.UpcomingInvoice.Date);

        Assert.NotNull(result.CustomerDiscount);
        Assert.Equal(StripeConstants.CouponIDs.Milestone2SubscriptionDiscount, result.CustomerDiscount.Id);
        Assert.True(result.CustomerDiscount.Active);
        Assert.Equal(20m, result.CustomerDiscount.PercentOff);
    }

    [Theory]
    [BitAutoData]
    public void Constructor_WithNullSubscriptionAndInvoice_HandlesNullsGracefully(
        User user,
        UserLicense license)
    {
        // Arrange - Test with null Subscription and UpcomingInvoice
        var subscriptionInfo = new SubscriptionInfo
        {
            Subscription = null,
            UpcomingInvoice = null,
            CustomerDiscount = new SubscriptionInfo.BillingCustomerDiscount
            {
                Id = StripeConstants.CouponIDs.Milestone2SubscriptionDiscount,
                Active = true,
                PercentOff = 20m
            }
        };

        // Act
        var result = new SubscriptionResponseModel(user, subscriptionInfo, license, includeDiscount: true);

        // Assert - Null Subscription and UpcomingInvoice should be handled gracefully
        Assert.Null(result.Subscription);
        Assert.Null(result.UpcomingInvoice);
        Assert.NotNull(result.CustomerDiscount);
    }
}
