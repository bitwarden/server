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


public class SeatSubscriptionUpdate : SubscriptionUpdate
{
    private readonly int _previousSeats;
    private readonly StaticStore.Plan _plan;
    private readonly long? _additionalSeats;
    protected override List<string> PlanIds => new() { _plan.StripeSeatPlanId };


    public SeatSubscriptionUpdate(Organization organization, StaticStore.Plan plan, long? additionalSeats)
    {
        _plan = plan;
        _additionalSeats = additionalSeats;
        _previousSeats = organization.Seats ?? 0;
    }

    public override List<SubscriptionItemOptions> UpgradeItemsOptions(Subscription subscription)
    {
        var item = SubscriptionItem(subscription, PlanIds.Single());
        return new()
        {
            new SubscriptionItemOptions
            {
                Id = item?.Id,
                Plan = PlanIds.Single(),
                Quantity = _additionalSeats,
                Deleted = (item?.Id != null && _additionalSeats == 0) ? true : (bool?)null,
            }
        };
    }

    public override List<SubscriptionItemOptions> RevertItemsOptions(Subscription subscription)
    {

        var item = SubscriptionItem(subscription, PlanIds.Single());
        return new()
        {
            new SubscriptionItemOptions
            {
                Id = item?.Id,
                Plan = PlanIds.Single(),
                Quantity = _previousSeats,
                Deleted = _previousSeats == 0 ? true : (bool?)null,
            }
        };
    }
}

public class StorageSubscriptionUpdate : SubscriptionUpdate
{
    private long? _prevStorage;
    private readonly string _plan;
    private readonly long? _additionalStorage;
    protected override List<string> PlanIds => new() { _plan };

    public StorageSubscriptionUpdate(string plan, long? additionalStorage)
    {
        _plan = plan;
        _additionalStorage = additionalStorage;
    }

    public override List<SubscriptionItemOptions> UpgradeItemsOptions(Subscription subscription)
    {
        var item = SubscriptionItem(subscription, PlanIds.Single());
        _prevStorage = item?.Quantity ?? 0;
        return new()
        {
            new SubscriptionItemOptions
            {
                Id = item?.Id,
                Plan = _plan,
                Quantity = _additionalStorage,
                Deleted = (item?.Id != null && _additionalStorage == 0) ? true : (bool?)null,
            }
        };
    }

    public override List<SubscriptionItemOptions> RevertItemsOptions(Subscription subscription)
    {
        if (!_prevStorage.HasValue)
        {
            throw new Exception("Unknown previous value, must first call UpgradeItemsOptions");
        }

        var item = SubscriptionItem(subscription, PlanIds.Single());
        return new()
        {
            new SubscriptionItemOptions
            {
                Id = item?.Id,
                Plan = _plan,
                Quantity = _prevStorage.Value,
                Deleted = _prevStorage.Value == 0 ? true : (bool?)null,
            }
        };
    }
}

public class SponsorOrganizationSubscriptionUpdate : SubscriptionUpdate
{
    private readonly string _existingPlanStripeId;
    private readonly string _sponsoredPlanStripeId;
    private readonly bool _applySponsorship;
    protected override List<string> PlanIds => new() { _existingPlanStripeId, _sponsoredPlanStripeId };

    public SponsorOrganizationSubscriptionUpdate(StaticStore.Plan existingPlan, StaticStore.SponsoredPlan sponsoredPlan, bool applySponsorship)
    {
        _existingPlanStripeId = existingPlan.StripePlanId;
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
