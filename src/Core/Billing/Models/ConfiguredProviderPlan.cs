using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Models;

public record ConfiguredProviderPlan(
    Guid Id,
    Guid ProviderId,
    Plan Plan,
    decimal Price,
    int SeatMinimum,
    int PurchasedSeats,
    int AssignedSeats);
