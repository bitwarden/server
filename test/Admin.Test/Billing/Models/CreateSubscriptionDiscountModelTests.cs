using Bit.Admin.Billing.Models;
using Bit.Core.Billing.Enums;

namespace Admin.Test.Billing.Models;

public class CreateSubscriptionDiscountModelTests
{
    [Fact]
    public void AudienceType_WhenCheckboxUnchecked_ReturnsAllUsers()
    {
        var model = new CreateSubscriptionDiscountModel
        {
            RestrictToNewUsersOnly = false
        };

        Assert.Equal(DiscountAudienceType.AllUsers, model.AudienceType);
    }

    [Fact]
    public void AudienceType_WhenCheckboxChecked_ReturnsUserHasNoPreviousSubscriptions()
    {
        var model = new CreateSubscriptionDiscountModel
        {
            RestrictToNewUsersOnly = true
        };

        Assert.Equal(DiscountAudienceType.UserHasNoPreviousSubscriptions, model.AudienceType);
    }

    [Fact]
    public void Validate_WhenEndDateBeforeStartDate_ReturnsError()
    {
        var model = new CreateSubscriptionDiscountModel
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
        var model = new CreateSubscriptionDiscountModel
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
        var model = new CreateSubscriptionDiscountModel
        {
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(10)
        };

        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var results = model.Validate(validationContext).ToList();

        Assert.Empty(results);
    }
}
