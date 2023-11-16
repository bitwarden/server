using Bit.Core.Entities;
using Stripe;

namespace Bit.Core.Models.Business;

public class PlanSubscriptionUpdate : SubscriptionUpdate
{
    private readonly int _previousSeats;
    private readonly StaticStore.Plan _plan;
    private readonly long? _seatCount;
    protected override List<string> PlanIds => new() { _plan.PasswordManager.StripeSeatPlanId };
    // If we're upgrading from 2019, we need to also remove the BASE plan ID, then add the SEAT ID.
    // protected override List<string> PlanIds => new() { _plan.PasswordManager.StripeStoragePlanId };
    public PlanSubscriptionUpdate(Organization organization, StaticStore.Plan plan, long? seatCount)
    {
        _plan = plan;
        _seatCount = seatCount;
        _previousSeats = organization.Seats.GetValueOrDefault();
    }

    public override List<SubscriptionItemOptions> UpgradeItemsOptions(Subscription subscription)
    {
        var item = SubscriptionItem(subscription, PlanIds.Single());
        return new List<SubscriptionItemOptions>
        {
            new()
            {
                Id = item?.Id,
                Plan = PlanIds.Single(),
                Quantity = _seatCount,
                Deleted = item?.Id != null && _seatCount == 0
                    ? true
                    : null
            }
        };
    }

    public override List<SubscriptionItemOptions> RevertItemsOptions(Subscription subscription)
    {
        var item = SubscriptionItem(subscription, PlanIds.Single());
        return new List<SubscriptionItemOptions>
        {
            new()
            {
                Id = item?.Id,
                Plan = PlanIds.Single(),
                Quantity = _previousSeats,
                Deleted = _previousSeats == 0
                    ? true
                    : null
            }
        };
    }
}
