using Bit.Core.Billing;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Stripe;

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
            throw new BillingException(
                message: $"Cannot create a {nameof(ProviderSubscriptionUpdate)} for {nameof(PlanType)} that doesn't support consolidated billing");
        }

        var plan = Utilities.StaticStore.GetPlan(planType);

        _planId = plan.PasswordManager.StripeProviderPortalSeatPlanId;
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
