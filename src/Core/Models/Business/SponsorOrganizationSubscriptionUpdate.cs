using Stripe;

namespace Bit.Core.Models.Business;

public class SponsorOrganizationSubscriptionUpdate : SubscriptionUpdate
{
    private readonly string _existingPlanStripeId;
    private readonly string _sponsoredPlanStripeId;
    private readonly bool _applySponsorship;
    protected override List<string> PlanIds => new() { _existingPlanStripeId, _sponsoredPlanStripeId };

    public SponsorOrganizationSubscriptionUpdate(StaticStore.Plan existingPlan, StaticStore.SponsoredPlan sponsoredPlan, bool applySponsorship)
    {
        _existingPlanStripeId = existingPlan.PasswordManager.StripePlanId;
        _sponsoredPlanStripeId = sponsoredPlan?.StripePlanId;
        _applySponsorship = applySponsorship;
    }

    public override List<SubscriptionItemOptions> RevertItemsOptions(Subscription subscription)
    {
        var result = new List<SubscriptionItemOptions>();
        if (!string.IsNullOrWhiteSpace(AddStripePlanId))
        {
            result.Add(new SubscriptionItemOptions
            {
                Id = AddStripeItem(subscription)?.Id,
                Plan = AddStripePlanId,
                Quantity = 0,
                Deleted = true,
            });
        }

        if (!string.IsNullOrWhiteSpace(RemoveStripePlanId))
        {
            result.Add(new SubscriptionItemOptions
            {
                Id = RemoveStripeItem(subscription)?.Id,
                Plan = RemoveStripePlanId,
                Quantity = 1,
                Deleted = false,
            });
        }
        return result;
    }

    public override List<SubscriptionItemOptions> UpgradeItemsOptions(Subscription subscription)
    {
        var result = new List<SubscriptionItemOptions>();
        if (RemoveStripeItem(subscription) != null)
        {
            result.Add(new SubscriptionItemOptions
            {
                Id = RemoveStripeItem(subscription)?.Id,
                Plan = RemoveStripePlanId,
                Quantity = 0,
                Deleted = true,
            });
        }

        if (!string.IsNullOrWhiteSpace(AddStripePlanId))
        {
            result.Add(new SubscriptionItemOptions
            {
                Id = AddStripeItem(subscription)?.Id,
                Plan = AddStripePlanId,
                Quantity = 1,
                Deleted = false,
            });
        }
        return result;
    }

    private string RemoveStripePlanId => _applySponsorship ? _existingPlanStripeId : _sponsoredPlanStripeId;
    private string AddStripePlanId => _applySponsorship ? _sponsoredPlanStripeId : _existingPlanStripeId;
    private Stripe.SubscriptionItem RemoveStripeItem(Subscription subscription) =>
        _applySponsorship ?
            SubscriptionItem(subscription, _existingPlanStripeId) :
            SubscriptionItem(subscription, _sponsoredPlanStripeId);
    private Stripe.SubscriptionItem AddStripeItem(Subscription subscription) =>
        _applySponsorship ?
            SubscriptionItem(subscription, _sponsoredPlanStripeId) :
            SubscriptionItem(subscription, _existingPlanStripeId);
}
