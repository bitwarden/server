using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Models;

public record ConfiguredProviderPlan(
    Guid Id,
    Guid ProviderId,
    PlanType PlanType,
    int SeatMinimum,
    int PurchasedSeats,
    int AssignedSeats
)
{
    public static ConfiguredProviderPlan From(ProviderPlan providerPlan) =>
        providerPlan.IsConfigured()
            ? new ConfiguredProviderPlan(
                providerPlan.Id,
                providerPlan.ProviderId,
                providerPlan.PlanType,
                providerPlan.SeatMinimum.GetValueOrDefault(0),
                providerPlan.PurchasedSeats.GetValueOrDefault(0),
                providerPlan.AllocatedSeats.GetValueOrDefault(0)
            )
            : null;
}
