using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using Bit.Core.Test.Billing.Mocks.Plans;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer.Models;

public class PendingAnnualUpgradeTests
{
    [Fact]
    public void PendingAnnualUpgrade_ExposesConstructorValues()
    {
        var plan = new TeamsPlan(true);
        var effectiveDate = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc);
        var lineItems = new List<PendingAnnualUpgradeLineItem>
        {
            new()
            {
                Name = "Teams (Annually) Seat",
                Amount = 48m,
                Quantity = 5,
                Interval = "year",
                ProductId = "prod_teams",
                AddonSubscriptionItem = true
            }
        };

        var pending = new PendingAnnualUpgrade
        {
            Plan = plan,
            LineItems = lineItems,
            EffectiveDate = effectiveDate
        };

        Assert.Same(plan, pending.Plan);
        Assert.Equal(effectiveDate, pending.EffectiveDate);
        var lineItem = Assert.Single(pending.LineItems);
        Assert.Equal("Teams (Annually) Seat", lineItem.Name);
        Assert.Equal(48m, lineItem.Amount);
        Assert.Equal(5, lineItem.Quantity);
        Assert.Equal("year", lineItem.Interval);
        Assert.Equal("prod_teams", lineItem.ProductId);
        Assert.True(lineItem.AddonSubscriptionItem);
    }

    [Fact]
    public void PendingAnnualUpgradeLineItem_Defaults_AreNullOrZero()
    {
        var lineItem = new PendingAnnualUpgradeLineItem();

        Assert.Null(lineItem.Name);
        Assert.Equal(0m, lineItem.Amount);
        Assert.Equal(0, lineItem.Quantity);
        Assert.Null(lineItem.Interval);
        Assert.Null(lineItem.ProductId);
        Assert.False(lineItem.AddonSubscriptionItem);
    }
}
