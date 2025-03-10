using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Models.Business;

public class ServiceAccountSubscriptionUpdateTests
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

    public void UpgradeItemsOptions_ReturnsCorrectOptions(PlanType planType, Organization organization)
    {
        var plan = StaticStore.GetPlan(planType);
        organization.PlanType = planType;
        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new ()
                    {
                        Id = "subscription_item",
                        Price = new Price { Id = plan.SecretsManager.StripeServiceAccountPlanId },
                        Quantity = 1
                    }
                }
            }
        };
        var update = new ServiceAccountSubscriptionUpdate(organization, plan, 3);

        var options = update.UpgradeItemsOptions(subscription);

        Assert.Single(options);
        Assert.Equal(plan.SecretsManager.StripeServiceAccountPlanId, options[0].Plan);
        Assert.Equal(3, options[0].Quantity);
        Assert.Null(options[0].Deleted);
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
    public void RevertItemsOptions_ReturnsCorrectOptions(PlanType planType, Organization organization)
    {
        var plan = StaticStore.GetPlan(planType);
        organization.PlanType = planType;
        var quantity = 5;
        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data = new List<SubscriptionItem>
                {
                    new ()
                    {
                        Id = "subscription_item",
                        Price = new Price { Id = plan.SecretsManager.StripeServiceAccountPlanId },
                        Quantity = quantity
                    }
                }
            }
        };
        var update = new ServiceAccountSubscriptionUpdate(organization, plan, quantity);
        update.UpgradeItemsOptions(subscription);

        var options = update.RevertItemsOptions(subscription);

        Assert.Single(options);
        Assert.Equal(plan.SecretsManager.StripeServiceAccountPlanId, options[0].Plan);
        Assert.Equal(quantity, options[0].Quantity);
        Assert.Null(options[0].Deleted);
    }
}
