using Bit.Core.Entities;
using Stripe;

namespace Bit.Core.Models.Business;

public abstract class BaseSeatSubscriptionUpdate : SubscriptionUpdate
{
    private readonly int _previousSeats;
    protected readonly StaticStore.Plan Plan;
    private readonly long? _additionalSeats;

    protected BaseSeatSubscriptionUpdate(Organization organization, StaticStore.Plan plan, long? additionalSeats, int previousSeats)
    {
        Plan = plan;
        _additionalSeats = additionalSeats;
        _previousSeats = previousSeats;
    }

    protected abstract string GetPlanId();

    protected override List<string> PlanIds => new() { GetPlanId() };

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
