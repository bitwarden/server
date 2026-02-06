using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Providers.Models;

public record ConfiguredProviderPlan(
    Guid Id,
    Guid ProviderId,
    Plan Plan,
    decimal Price,
    int SeatMinimum,
    int PurchasedSeats,
    int AssignedSeats);
