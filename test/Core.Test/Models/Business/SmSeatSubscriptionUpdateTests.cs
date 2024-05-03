using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Models.Business;

public class SmSeatSubscriptionUpdateTests
{
    [Theory]
    [BitAutoData(PlanType.EnterpriseMonthly2019)]
    [BitAutoData(PlanType.EnterpriseMonthly2020)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually2019)]
    [BitAutoData(PlanType.EnterpriseAnnually2020)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.TeamsMonthly2019)]
    [BitAutoData(PlanType.TeamsMonthly2020)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually2019)]
    [BitAutoData(PlanType.TeamsAnnually2020)]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsStarter)]

    public void UpgradeItemsOptions_ReturnsCorrectOptions(PlanType planType)
    {
        var plan = StaticStore.GetPlan(planType);
        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new ()
                    {
                        Id = "subscription_item",
                        Price = new Price { Id = plan.SecretsManager.StripeSeatPlanId },
                        Quantity = 1
                    }
                }
            }
        };
        var update = new StorageSubscriptionUpdate(plan.SecretsManager.StripeSeatPlanId, 3);

        var options = update.UpgradeItemsOptions(subscription);

        Assert.Single(options);
        Assert.Equal(plan.SecretsManager.StripeSeatPlanId, options[0].Plan);
        Assert.Equal(3, options[0].Quantity);
        Assert.Null(options[0].Deleted);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    public void RevertItemsOptions_ThrowsExceptionIfPrevStorageIsNull(PlanType planType)
    {
        var plan = StaticStore.GetPlan(planType);
        var subscription = new Subscription();
        var update = new StorageSubscriptionUpdate(plan.SecretsManager.StripeSeatPlanId, 3);

        Assert.Throws<Exception>(() => update.RevertItemsOptions(subscription));
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseMonthly2019)]
    [BitAutoData(PlanType.EnterpriseMonthly2020)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually2019)]
    [BitAutoData(PlanType.EnterpriseAnnually2020)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.TeamsMonthly2019)]
    [BitAutoData(PlanType.TeamsMonthly2020)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsAnnually2019)]
    [BitAutoData(PlanType.TeamsAnnually2020)]
    [BitAutoData(PlanType.TeamsAnnually)]
    public void RevertItemsOptions_ReturnsCorrectOptions(PlanType planType)
    {
        var plan = StaticStore.GetPlan(planType);
        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new ()
                    {
                        Id = "subscription_item",
                        Price = new Price { Id = plan.SecretsManager.StripeSeatPlanId },
                        Quantity = 5
                    }
                }
            }
        };
        var update = new StorageSubscriptionUpdate(plan.SecretsManager.StripeSeatPlanId, 5);
        update.UpgradeItemsOptions(subscription);

        var options = update.RevertItemsOptions(subscription);

        Assert.Single(options);
        Assert.Equal(plan.SecretsManager.StripeSeatPlanId, options[0].Plan);
        Assert.Equal(5, options[0].Quantity);
        Assert.Null(options[0].Deleted);
    }
}
