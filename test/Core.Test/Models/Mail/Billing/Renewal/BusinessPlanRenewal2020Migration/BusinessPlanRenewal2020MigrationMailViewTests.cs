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

    [Fact]
    public void ShowProactiveDiscountCopy_IsFalse_WhenNoMonths()
    {
        var view = BuildView(proactiveDiscountMonths: 0);

        Assert.False(view.ShowProactiveDiscountCopy);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void ShowProactiveDiscountCopy_IsTrue_WhenPositiveMonths(int months)
    {
        var view = BuildView(proactiveDiscountMonths: months);

        Assert.True(view.ShowProactiveDiscountCopy);
    }

    [Theory]
    [InlineData(1, "next month")]
    [InlineData(12, "next 12 months")]
    public void ProactiveDiscountDurationPhrase_FollowsMonthCount(int months, string expected)
    {
        var view = BuildView(proactiveDiscountMonths: months);

        Assert.Equal(expected, view.ProactiveDiscountDurationPhrase);
    }

    private static BusinessPlanRenewal2020MigrationMailView BuildView(
        List<string>? discountLines = null,
        bool isAnnual = true,
        int proactiveDiscountMonths = 0) =>
        new()
        {
            RenewalDate = "June 12, 2026",
            Seats = 320,
            PerUserMonthlyPrice = "$6.00",
            IsAnnual = isAnnual,
            TotalPrice = "$23,040.00",
            DiscountLines = discountLines ?? [],
            ProactiveDiscountMonths = proactiveDiscountMonths
        };
}
