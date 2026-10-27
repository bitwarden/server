using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Providers.Entities;
using Bit.Core.Utilities;
using Provider = Bit.Core.AdminConsole.Entities.Provider.Provider;

namespace Bit.Seeder.Factories;

internal static class ProviderPlanSeeder
{
    /// <summary>
    /// Creates a configured ProviderPlan for the provider. Mirrors <c>CreateProviderCommand</c> and the provider
    /// billing seat scaling: seats allocated beyond <paramref name="seatMinimum"/> are counted as purchased.
    /// </summary>
    internal static ProviderPlan Create(Provider provider, PlanType planType, int seatMinimum, int allocatedSeats)
    {
        return new ProviderPlan
        {
            Id = CombGuid.Generate(),
            ProviderId = provider.Id,
            PlanType = planType,
            SeatMinimum = seatMinimum,
            PurchasedSeats = Math.Max(0, allocatedSeats - seatMinimum),
            AllocatedSeats = allocatedSeats
        };
    }
}
