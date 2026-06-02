using Bit.Core.Models.Mail.Billing.Renewal.BusinessPlanRenewal2020Migration;
using Xunit;

namespace Bit.Core.Test.Models.Mail.Billing.Renewal.BusinessPlanRenewal2020Migration;

public class BusinessPlanRenewal2020MigrationMailViewTests
{
    [Theory]
    [InlineData("20%", true)]
    [InlineData("0%", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", true)]
    public void HasDiscount_ReflectsWhetherDiscountPercentIsPopulated(string? discountPercent, bool expected)
    {
        var view = BuildView(discountPercent: discountPercent);

        Assert.Equal(expected, view.HasDiscount);
    }

    [Theory]
    [InlineData(true, "year")]
    [InlineData(false, "month")]
    public void TotalPeriod_FollowsCadence(bool isAnnual, string expected)
    {
        var view = BuildView(isAnnual: isAnnual);

        Assert.Equal(expected, view.TotalPeriod);
    }

    private static BusinessPlanRenewal2020MigrationMailView BuildView(
        string? discountPercent = null,
        bool isAnnual = true) =>
        new()
        {
            RenewalDate = "June 12, 2026",
            Seats = 320,
            PerUserMonthlyPrice = "$6.00",
            IsAnnual = isAnnual,
            TotalPrice = "$23,040.00",
            DiscountPercent = discountPercent
        };
}
