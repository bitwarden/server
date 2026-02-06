using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Providers.Models;

/// <param name="Provider">The provider to update the seat minimums for.</param>
/// <param name="Configuration">The new seat minimums for the provider.</param>
public record UpdateProviderSeatMinimumsCommand(
    Provider Provider,
    IReadOnlyCollection<(PlanType Plan, int SeatsMinimum)> Configuration);
