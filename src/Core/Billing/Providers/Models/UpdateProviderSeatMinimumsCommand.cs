using Bit.Core.Billing.Enums;
using Bit.Core.Providers.Entities;

namespace Bit.Core.Billing.Providers.Models;

/// <param name="Provider">The provider to update the seat minimums for.</param>
/// <param name="Configuration">The new seat minimums for the provider.</param>
public record UpdateProviderSeatMinimumsCommand(
    Provider Provider,
    IReadOnlyCollection<(PlanType Plan, int SeatsMinimum)> Configuration);
