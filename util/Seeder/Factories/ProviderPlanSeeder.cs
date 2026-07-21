using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Providers.Entities;
using Bit.Core.Utilities;
using Provider = Bit.Core.AdminConsole.Entities.Provider.Provider;

namespace Bit.Seeder.Factories;

internal static class ProviderPlanSeeder
{
    /// <summary>
    /// Creates a configured ProviderPlan for the provider. Seat minimum, purchased, and allocated seats
    /// are all set to <paramref name="seats"/> so that <see cref="ProviderPlan.IsConfigured"/> returns true.
    /// </summary>
    internal static ProviderPlan Create(Provider provider, PlanType planType, int seats)
    {
        return new ProviderPlan
        {
            Id = CoreHelpers.GenerateComb(),
            ProviderId = provider.Id,
            PlanType = planType,
            SeatMinimum = seats,
            PurchasedSeats = seats,
            AllocatedSeats = seats
        };
    }
}
