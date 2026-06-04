using Bit.Core.Models.Mail.Billing.Renewal.BusinessPlanRenewal2020Migration;
using Xunit;

namespace Bit.Core.Test.Models.Mail.Billing.Renewal.BusinessPlanRenewal2020Migration;

public class BusinessPlanRenewal2020MigrationMailViewTests
{
    [Fact]
    public void HasDiscount_IsFalse_WhenNoDiscounts()
    {
        var view = BuildView(discountLines: []);

        Assert.False(view.HasDiscount);
    }

    [Fact]
    public void HasDiscount_IsTrue_WhenSinglePercentageDiscount()
    {
        var view = BuildView(discountLines: ["20%"]);

        Assert.True(view.HasDiscount);
    }

    [Fact]
    public void HasDiscount_IsTrue_WhenFixedAmountDiscount()
    {
        var view = BuildView(discountLines: ["$50.00"]);

        Assert.True(view.HasDiscount);
    }

    [Fact]
    public void HasDiscount_IsTrue_WhenMixedDiscounts()
    {
        var view = BuildView(discountLines: ["20%", "$50.00"]);

        Assert.True(view.HasDiscount);
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
        List<string>? discountLines = null,
        bool isAnnual = true) =>
        new()
        {
            RenewalDate = "June 12, 2026",
            Seats = 320,
            PerUserMonthlyPrice = "$6.00",
            IsAnnual = isAnnual,
            TotalPrice = "$23,040.00",
            DiscountLines = discountLines ?? []
        };
}
