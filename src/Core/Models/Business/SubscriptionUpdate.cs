using Bit.Core.Billing.Enums;
using Stripe;

namespace Bit.Core.Models.Business;

public abstract class SubscriptionUpdate
{
    protected abstract List<string> PlanIds { get; }

    public abstract List<SubscriptionItemOptions> RevertItemsOptions(Subscription subscription);
    public abstract List<SubscriptionItemOptions> UpgradeItemsOptions(Subscription subscription);

    public virtual bool UpdateNeeded(Subscription subscription)
    {
        var upgradeItemsOptions = UpgradeItemsOptions(subscription);
        foreach (var upgradeItemOptions in upgradeItemsOptions)
        {
            var upgradeQuantity = upgradeItemOptions.Quantity ?? 0;
            var existingQuantity = FindSubscriptionItem(subscription, upgradeItemOptions.Plan)?.Quantity ?? 0;
            if (upgradeQuantity != existingQuantity)
            {
                return true;
            }
        }
        return false;
    }

    protected static SubscriptionItem FindSubscriptionItem(Subscription subscription, string planId)
    {
        if (string.IsNullOrEmpty(planId))
        {
            return null;
        }

        var data = subscription.Items.Data;

        var subscriptionItem = data.FirstOrDefault(item => item.Plan?.Id == planId) ?? data.FirstOrDefault(item => item.Price?.Id == planId);

        return subscriptionItem;
    }

    protected static string GetPasswordManagerPlanId(StaticStore.Plan plan)
        => IsNonSeatBasedPlan(plan)
            ? plan.PasswordManager.StripePlanId
            : plan.PasswordManager.StripeSeatPlanId;

    protected static bool IsNonSeatBasedPlan(StaticStore.Plan plan)
        => plan.Type is
            >= PlanType.FamiliesAnnually2019 and <= PlanType.EnterpriseAnnually2019
            or PlanType.FamiliesAnnually
            or PlanType.TeamsStarter2023
            or PlanType.TeamsStarter;
}
