using Bit.Core.Entities;

namespace Bit.Core.Models.Business;

public class SeatSubscriptionUpdate :BaseSeatSubscriptionUpdate
{
    public SeatSubscriptionUpdate(Organization organization, StaticStore.Plan plan, long? additionalSeats)
        : base(organization, plan, additionalSeats, organization.Seats.GetValueOrDefault())
    { }

    protected override string GetPlanId() => Plan.PasswordManager.StripeSeatPlanId;
}
