using Bit.Core.Entities;
using Stripe;

namespace Bit.Core.Models.Business;

public abstract class SubscriptionUpdate
{
    protected abstract List<string> PlanIds { get; }

    public abstract List<SubscriptionItemOptions> RevertItemsOptions(Subscription subscription);
    public abstract List<SubscriptionItemOptions> UpgradeItemsOptions(Subscription subscription);

    public bool UpdateNeeded(Subscription subscription)
    {
        var upgradeItemsOptions = UpgradeItemsOptions(subscription);
        foreach (var upgradeItemOptions in upgradeItemsOptions)
        {
            var upgradeQuantity = upgradeItemOptions.Quantity ?? 0;
            var existingQuantity = SubscriptionItem(subscription, upgradeItemOptions.Plan)?.Quantity ?? 0;
            if (upgradeQuantity != existingQuantity)
            {
                return true;
            }
        }
        return false;
    }

    protected static SubscriptionItem SubscriptionItem(Subscription subscription, string planId) =>
        planId == null ? null : subscription.Items?.Data?.FirstOrDefault(i => i.Plan.Id == planId);
}
