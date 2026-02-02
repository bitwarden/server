using System.Text.Json;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Subscriptions.Entities;
using Xunit;

namespace Bit.Core.Test.Billing.Subscriptions.Entities;

public class SubscriptionDiscountTests
{
    [Fact]
    public void StripeProductIds_CanSerializeToJson()
    {
        // Arrange
        var discount = new SubscriptionDiscount
        {
            StripeCouponId = "test-coupon",
            StripeProductIds = new List<string> { "prod_123", "prod_456" },
            Duration = "once",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions
        };

        // Act
        var json = JsonSerializer.Serialize(discount.StripeProductIds);

        // Assert
        Assert.Equal("[\"prod_123\",\"prod_456\"]", json);
    }

    [Fact]
    public void StripeProductIds_CanDeserializeFromJson()
    {
        // Arrange
        var json = "[\"prod_123\",\"prod_456\"]";

        // Act
        var result = JsonSerializer.Deserialize<List<string>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains("prod_123", result);
        Assert.Contains("prod_456", result);
    }

    [Fact]
    public void StripeProductIds_HandlesNull()
    {
        // Arrange
        var discount = new SubscriptionDiscount
        {
            StripeCouponId = "test-coupon",
            StripeProductIds = null,
            Duration = "once",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions
        };

        // Act
        var json = JsonSerializer.Serialize(discount.StripeProductIds);

        // Assert
        Assert.Equal("null", json);
    }

    [Fact]
    public void StripeProductIds_HandlesEmptyCollection()
    {
        // Arrange
        var discount = new SubscriptionDiscount
        {
            StripeCouponId = "test-coupon",
            StripeProductIds = new List<string>(),
            Duration = "once",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions
        };

        // Act
        var json = JsonSerializer.Serialize(discount.StripeProductIds);

        // Assert
        Assert.Equal("[]", json);
    }

    [Fact]
    public void Validate_RejectsEndDateBeforeStartDate()
    {
        // Arrange
        var discount = new SubscriptionDiscount
        {
            StripeCouponId = "test-coupon",
            Duration = "once",
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow, // EndDate before StartDate
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions
        };

        // Act
        var validationResults = discount.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(discount)).ToList();

        // Assert
        Assert.Single(validationResults);
        Assert.Contains("EndDate", validationResults[0].MemberNames);
    }
}
