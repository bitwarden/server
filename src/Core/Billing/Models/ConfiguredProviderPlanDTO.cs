using Bit.Core.Billing.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Billing.Models;

public record ConfiguredProviderPlanDTO(
    Guid Id,
    Guid ProviderId,
    PlanType PlanType,
    int SeatMinimum,
    int PurchasedSeats,
    int AssignedSeats)
{
    public static ConfiguredProviderPlanDTO From(ProviderPlan providerPlan) =>
        providerPlan.IsConfigured()
            ? new ConfiguredProviderPlanDTO(
                providerPlan.Id,
                providerPlan.ProviderId,
                providerPlan.PlanType,
                providerPlan.SeatMinimum.GetValueOrDefault(0),
                providerPlan.PurchasedSeats.GetValueOrDefault(0),
                providerPlan.AllocatedSeats.GetValueOrDefault(0))
            : null;
}
