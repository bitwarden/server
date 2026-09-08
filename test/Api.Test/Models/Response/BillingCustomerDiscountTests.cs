using Bit.Api.Models.Response;
using Bit.Core.Models.Business;
using Stripe;
using Xunit;

namespace Bit.Api.Test.Models.Response;

public class BillingCustomerDiscountTests
{
    [Fact]
    public void Constructor_FromDomainDiscount_MirrorsEndDurationAndLeavesActiveIntact()
    {
        // Arrange - domain discount built from a Stripe Discount with an end date
        var end = DateTime.UtcNow.AddMonths(12);
        var domainDiscount = new SubscriptionInfo.BillingCustomerDiscount(new Discount
        {
            End = end,
            Source = new DiscountSource
            {
                Coupon = new Coupon
                {
                    Id = "churn_15_repeating",
                    PercentOff = 10m,
                    Duration = "repeating",
                    DurationInMonths = 12
                }
            }
        });

        // Act
        var result = new BillingCustomerDiscount(domainDiscount);

        // Assert - API model copies End/DurationInMonths and preserves Active unchanged
        Assert.Equal(domainDiscount.End, result.End);
        Assert.Equal(domainDiscount.DurationInMonths, result.DurationInMonths);
        Assert.Equal(domainDiscount.Active, result.Active);
        Assert.False(result.Active);   // End != null => not perpetual (UNCHANGED semantics)
        Assert.Equal(domainDiscount.Id, result.Id);
        Assert.Equal(domainDiscount.PercentOff, result.PercentOff);
        Assert.Equal(domainDiscount.AmountOff, result.AmountOff);
    }

    [Fact]
    public void Constructor_FromPerpetualDomainDiscount_EndNullAndActiveTrue()
    {
        // Arrange - perpetual domain discount (no end date)
        var domainDiscount = new SubscriptionInfo.BillingCustomerDiscount(new Discount
        {
            End = null,
            Source = new DiscountSource
            {
                Coupon = new Coupon
                {
                    Id = "perpetual_10",
                    PercentOff = 10m,
                    Duration = "forever"
                }
            }
        });

        // Act
        var result = new BillingCustomerDiscount(domainDiscount);

        // Assert
        Assert.Null(result.End);
        Assert.True(result.Active);   // End == null => perpetual (UNCHANGED semantics)
    }
}
