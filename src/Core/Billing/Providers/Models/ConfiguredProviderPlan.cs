using Bit.Core.Billing.Pricing.Static;

namespace Bit.Core.Billing.Providers.Models;

public record ConfiguredProviderPlan(
    Guid Id,
    Guid ProviderId,
    Plan Plan,
    int SeatMinimum,
    int PurchasedSeats,
    int AssignedSeats);
