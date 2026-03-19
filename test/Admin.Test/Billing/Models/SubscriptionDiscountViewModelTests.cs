using Bit.Admin.Billing.Models;
using Bit.Core.Billing.Enums;

namespace Admin.Test.Billing.Models;

public class SubscriptionDiscountViewModelTests
{
    [Fact]
    public void DiscountDisplay_WithPercentOff_ReturnsFormattedPercent()
    {
        var model = new SubscriptionDiscountViewModel
        {
            PercentOff = 25m
        };

        Assert.Equal("25% off", model.DiscountDisplay);
    }

    [Fact]
    public void DiscountDisplay_WithDecimalPercentOff_ReturnsFormattedPercentWithDecimals()
    {
        var model = new SubscriptionDiscountViewModel
        {
            PercentOff = 33.5m
        };

        Assert.Equal("33.5% off", model.DiscountDisplay);
    }

    [Fact]
    public void DiscountDisplay_WithWholeNumberPercentOff_ReturnsFormattedPercentWithoutDecimals()
    {
        var model = new SubscriptionDiscountViewModel
        {
            PercentOff = 50.00m
        };

        Assert.Equal("50% off", model.DiscountDisplay);
    }

    [Fact]
    public void DiscountDisplay_WithAmountOff_ReturnsFormattedDollar()
    {
        var model = new SubscriptionDiscountViewModel
        {
            AmountOff = 1000
        };

        Assert.Equal("$10 off", model.DiscountDisplay);
    }

    [Fact]
    public void DiscountDisplay_WithZeroAmountOff_ReturnsZero()
    {
        var model = new SubscriptionDiscountViewModel
        {
            AmountOff = 0
        };

        Assert.Equal("$0 off", model.DiscountDisplay);
    }

    [Fact]
    public void IsRestrictedToNewUsersOnly_WithMatchingAudienceType_ReturnsTrue()
    {
        var model = new SubscriptionDiscountViewModel
        {
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions
        };

        Assert.True(model.IsRestrictedToNewUsersOnly);
    }

    [Fact]
    public void IsRestrictedToNewUsersOnly_WithAllUsersAudienceType_ReturnsFalse()
    {
        var model = new SubscriptionDiscountViewModel
        {
            AudienceType = DiscountAudienceType.AllUsers
        };

        Assert.False(model.IsRestrictedToNewUsersOnly);
    }

    [Fact]
    public void IsAvailableToAllUsers_WithAllUsersAudienceType_ReturnsTrue()
    {
        var model = new SubscriptionDiscountViewModel
        {
            AudienceType = DiscountAudienceType.AllUsers
        };

        Assert.True(model.IsAvailableToAllUsers);
    }

    [Fact]
    public void IsAvailableToAllUsers_WithRestrictedAudienceType_ReturnsFalse()
    {
        var model = new SubscriptionDiscountViewModel
        {
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions
        };

        Assert.False(model.IsAvailableToAllUsers);
    }

    [Fact]
    public void IsActive_WhenWithinDateRange_ReturnsTrue()
    {
        var model = new SubscriptionDiscountViewModel
        {
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1)
        };

        Assert.True(model.IsActive);
    }

    [Fact]
    public void IsActive_WhenBeforeStartDate_ReturnsFalse()
    {
        var model = new SubscriptionDiscountViewModel
        {
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2)
        };

        Assert.False(model.IsActive);
    }

    [Fact]
    public void IsActive_WhenAfterEndDate_ReturnsFalse()
    {
        var model = new SubscriptionDiscountViewModel
        {
            StartDate = DateTime.UtcNow.AddDays(-2),
            EndDate = DateTime.UtcNow.AddDays(-1)
        };

        Assert.False(model.IsActive);
    }

    [Fact]
    public void IsActive_WhenExactlyOnStartDate_ReturnsTrue()
    {
        var now = DateTime.UtcNow;
        var model = new SubscriptionDiscountViewModel
        {
            StartDate = now,
            EndDate = now.AddDays(1)
        };

        Assert.True(model.IsActive);
    }

    [Fact]
    public void IsActive_WhenCurrentTimeIsOnEndDate_ReturnsTrue()
    {
        var now = DateTime.UtcNow;
        var model = new SubscriptionDiscountViewModel
        {
            StartDate = now.AddDays(-1),
            EndDate = now.AddSeconds(1)
        };

        Assert.True(model.IsActive);
    }
}
