using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Services.Contracts;

/// <param name="Id">The ID of the provider to update the seat minimums for.</param>
/// <param name="Configuration">The new seat minimums for the provider.</param>
public record UpdateProviderSeatMinimumsCommand(
    Guid Id,
    string GatewaySubscriptionId,
    IReadOnlyCollection<(PlanType Plan, int SeatsMinimum)> Configuration
);
