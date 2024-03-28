using Bit.Core.Billing.Extensions;
using Bit.Core.Enums;
using Stripe;

using static Bit.Core.Billing.Utilities;

namespace Bit.Core.Models.Business;

public class ProviderSubscriptionUpdate : SubscriptionUpdate
{
    private readonly string _planId;
    private readonly int _previouslyPurchasedSeats;
    private readonly int _newlyPurchasedSeats;

    protected override List<string> PlanIds => [_planId];

    public ProviderSubscriptionUpdate(
        PlanType planType,
        int previouslyPurchasedSeats,
        int newlyPurchasedSeats)
    {
        if (!planType.SupportsConsolidatedBilling())
        {
            throw ContactSupport($"Cannot create a {nameof(ProviderSubscriptionUpdate)} for {nameof(PlanType)} that doesn't support consolidated billing");
        }

        _planId = GetPasswordManagerPlanId(Utilities.StaticStore.GetPlan(planType));
        _previouslyPurchasedSeats = previouslyPurchasedSeats;
        _newlyPurchasedSeats = newlyPurchasedSeats;
    }

    public override List<SubscriptionItemOptions> RevertItemsOptions(Subscription subscription)
    {
        var subscriptionItem = FindSubscriptionItem(subscription, _planId);

        return
        [
            new SubscriptionItemOptions
            {
                Id = subscriptionItem.Id,
                Price = _planId,
                Quantity = _previouslyPurchasedSeats
            }
        ];
    }

    public override List<SubscriptionItemOptions> UpgradeItemsOptions(Subscription subscription)
    {
        var subscriptionItem = FindSubscriptionItem(subscription, _planId);

        return
        [
            new SubscriptionItemOptions
            {
                Id = subscriptionItem.Id,
                Price = _planId,
                Quantity = _newlyPurchasedSeats
            }
        ];
    }
}
