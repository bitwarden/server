using Bit.Core.AdminConsole.Entities;
using Stripe;

namespace Bit.Core.Models.Business;

public class SmSeatSubscriptionUpdate : SubscriptionUpdate
{
    private readonly int _previousSeats;
    private readonly StaticStore.Plan _plan;
    private readonly long? _additionalSeats;
    protected override List<string> PlanIds => new() { _plan.SecretsManager.StripeSeatPlanId };

    public SmSeatSubscriptionUpdate(Organization organization, StaticStore.Plan plan, long? additionalSeats)
    {
        _plan = plan;
        _additionalSeats = additionalSeats;
        _previousSeats = organization.SmSeats.GetValueOrDefault();
    }

    public override List<SubscriptionItemOptions> UpgradeItemsOptions(Subscription subscription)
    {
        var item = FindSubscriptionItem(subscription, PlanIds.Single());
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

        var item = FindSubscriptionItem(subscription, PlanIds.Single());
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
