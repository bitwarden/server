using Bit.Admin.Billing.Models;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Subscriptions.Entities;

namespace Admin.Test.Billing.Models;

public class EditSubscriptionDiscountModelTests
{
    [Fact]
    public void AudienceType_WhenRestrictToNewUsersOnly_ReturnsUserHasNoPreviousSubscriptions()
    {
        var model = new EditSubscriptionDiscountModel
        {
            RestrictToNewUsersOnly = true
        };

        Assert.Equal(DiscountAudienceType.UserHasNoPreviousSubscriptions, model.AudienceType);
    }

    [Fact]
    public void AudienceType_WhenNotRestricted_ReturnsAllUsers()
    {
        var model = new EditSubscriptionDiscountModel
        {
            RestrictToNewUsersOnly = false
        };

        Assert.Equal(DiscountAudienceType.AllUsers, model.AudienceType);
    }

    [Fact]
    public void Validate_WhenEndDateBeforeStartDate_ReturnsError()
    {
        var model = new EditSubscriptionDiscountModel
        {
            StartDate = DateTime.UtcNow.Date.AddDays(10),
            EndDate = DateTime.UtcNow.Date
        };

        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var results = model.Validate(validationContext).ToList();

        Assert.Single(results);
        Assert.Contains("End Date must be on or after Start Date", results[0].ErrorMessage);
        Assert.Contains(nameof(model.EndDate), results[0].MemberNames);
    }

    [Fact]
    public void Validate_WhenEndDateEqualsStartDate_NoError()
    {
        var model = new EditSubscriptionDiscountModel
        {
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date
        };

        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var results = model.Validate(validationContext).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WhenEndDateAfterStartDate_NoError()
    {
        var model = new EditSubscriptionDiscountModel
        {
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(10)
        };

        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var results = model.Validate(validationContext).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void Constructor_FromEntity_MapsAllProperties()
    {
        var discount = new SubscriptionDiscount
        {
            Id = Guid.NewGuid(),
            StripeCouponId = "COUPON123",
            Name = "Test Coupon",
            PercentOff = 25m,
            AmountOff = null,
            Currency = "usd",
            Duration = "once",
            DurationInMonths = null,
            StripeProductIds = new List<string> { "prod_1", "prod_2" },
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 12, 31),
            AudienceType = DiscountAudienceType.AllUsers
        };

        var model = new EditSubscriptionDiscountModel(discount);

        Assert.Equal(discount.Id, model.Id);
        Assert.Equal(discount.StripeCouponId, model.StripeCouponId);
        Assert.Equal(discount.Name, model.Name);
        Assert.Equal(discount.PercentOff, model.PercentOff);
        Assert.Equal(discount.AmountOff, model.AmountOff);
        Assert.Equal(discount.Currency, model.Currency);
        Assert.Equal(discount.Duration, model.Duration);
        Assert.Equal(discount.DurationInMonths, model.DurationInMonths);
        Assert.Equal(discount.StripeProductIds, model.StripeProductIds);
        Assert.Equal(discount.StartDate, model.StartDate);
        Assert.Equal(discount.EndDate, model.EndDate);
    }

    [Fact]
    public void Constructor_FromEntity_WhenAudienceTypeIsUserHasNoPreviousSubscriptions_SetsRestrictToNewUsersOnlyTrue()
    {
        var discount = new SubscriptionDiscount
        {
            StripeCouponId = "COUPON123",
            Duration = "once",
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddMonths(1),
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions
        };

        var model = new EditSubscriptionDiscountModel(discount);

        Assert.True(model.RestrictToNewUsersOnly);
    }

    [Fact]
    public void Constructor_FromEntity_WhenAudienceTypeIsAllUsers_SetsRestrictToNewUsersOnlyFalse()
    {
        var discount = new SubscriptionDiscount
        {
            StripeCouponId = "COUPON123",
            Duration = "once",
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddMonths(1),
            AudienceType = DiscountAudienceType.AllUsers
        };

        var model = new EditSubscriptionDiscountModel(discount);

        Assert.False(model.RestrictToNewUsersOnly);
    }
}
