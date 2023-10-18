using Bit.Core.Entities;

namespace Bit.Core.Models.Business;

public class SmSeatSubscriptionUpdate : BaseSeatSubscriptionUpdate
{
    public SmSeatSubscriptionUpdate(Organization organization, StaticStore.Plan plan, long? additionalSeats)
        : base(organization, plan, additionalSeats, organization.SmSeats.GetValueOrDefault())
    { }

    protected override string GetPlanId() => Plan.SecretsManager.StripeSeatPlanId;
}
