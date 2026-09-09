using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using Bit.Core.Models.Business;
using Bit.Core.Test.Billing.Mocks.Plans;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.AnnualUpgradeOffer.Models;

public class PendingAnnualUpgradeTests
{
    [Fact]
    public void PendingAnnualUpgrade_ExposesConstructorValues()
    {
        var plan = new TeamsPlan(true);
        var effectiveDate = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc);
        var lineItem = new SubscriptionInfo.BillingSubscription.BillingSubscriptionItem(
            new SubscriptionSchedulePhaseItem
            {
                Quantity = 5,
                Price = new Price
                {
                    Id = "price_1",
                    ProductId = "prod_teams",
                    Nickname = "Teams (Annually) Seat",
                    UnitAmount = 4800,
                    Recurring = new PriceRecurring { Interval = "year" },
                    Metadata = new Dictionary<string, string> { ["isAddOn"] = "true" }
                }
            });
        var lineItems = new List<SubscriptionInfo.BillingSubscription.BillingSubscriptionItem> { lineItem };

        var pending = new PendingAnnualUpgrade
        {
            Plan = plan,
            LineItems = lineItems,
            EffectiveDate = effectiveDate
        };

        Assert.Same(plan, pending.Plan);
        Assert.Equal(effectiveDate, pending.EffectiveDate);
        var resultLineItem = Assert.Single(pending.LineItems);
        Assert.Equal("Teams (Annually) Seat", resultLineItem.Name);
        Assert.Equal(48m, resultLineItem.Amount);
        Assert.Equal(5, resultLineItem.Quantity);
        Assert.Equal("year", resultLineItem.Interval);
        Assert.Equal("prod_teams", resultLineItem.ProductId);
        Assert.True(resultLineItem.AddonSubscriptionItem);
    }
}
