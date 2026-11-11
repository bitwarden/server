using System.ComponentModel.DataAnnotations;
using Bit.Admin.Billing.Models;
using Bit.Core.Billing.Organizations.Models;

namespace Admin.Test.Billing.Models;

public class ExtendTrialModelTests
{
    private static List<ValidationResult> Validate(ExtendTrialModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Validate_DaysMissing_ReturnsRequiredMessage()
    {
        var model = new ExtendTrialModel { Days = null };

        var results = Validate(model);

        var result = Assert.Single(results);
        Assert.Equal(TrialExtensionPolicy.DaysRequiredMessage, result.ErrorMessage);
        Assert.Contains(nameof(ExtendTrialModel.Days), result.MemberNames);
    }

    [Theory]
    [InlineData(TrialExtensionPolicy.MinExtensionDays - 1)]
    [InlineData(TrialExtensionPolicy.MaxExtensionDays + 1)]
    [InlineData(-5)]
    [InlineData(365)]
    public void Validate_DaysOutOfRange_ReturnsOutOfRangeMessage(int days)
    {
        var model = new ExtendTrialModel { Days = days };

        var results = Validate(model);

        var result = Assert.Single(results);
        Assert.Equal(TrialExtensionPolicy.DaysOutOfRangeMessage, result.ErrorMessage);
        Assert.Contains(nameof(ExtendTrialModel.Days), result.MemberNames);
    }

    [Theory]
    [InlineData(TrialExtensionPolicy.MinExtensionDays)]
    [InlineData(15)]
    [InlineData(TrialExtensionPolicy.MaxExtensionDays)]
    public void Validate_DaysWithinRange_NoErrors(int days)
    {
        var model = new ExtendTrialModel { Days = days };

        var results = Validate(model);

        Assert.Empty(results);
    }
}
