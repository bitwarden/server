using Bit.Core.Billing.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Billing.Models;

public record ConfiguredProviderPlan(
    Guid Id,
    Guid ProviderId,
    PlanType PlanType,
    int SeatMinimum,
    int PurchasedSeats)
{
    public static ConfiguredProviderPlan From(ProviderPlan providerPlan) =>
        providerPlan.Configured
            ? new ConfiguredProviderPlan(
                providerPlan.Id,
                providerPlan.ProviderId,
                providerPlan.PlanType,
                providerPlan.SeatMinimum.GetValueOrDefault(0),
                providerPlan.PurchasedSeats.GetValueOrDefault(0))
            : null;
}
