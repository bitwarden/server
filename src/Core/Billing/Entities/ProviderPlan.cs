using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Billing.Entities;

public class ProviderPlan : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public PlanType PlanType { get; set; }
    public int? SeatMinimum { get; set; }
    public int? PurchasedSeats { get; set; }
    public int? AllocatedSeats { get; set; }

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }

    public bool IsConfigured() => SeatMinimum.HasValue && PurchasedSeats.HasValue && AllocatedSeats.HasValue;
}
