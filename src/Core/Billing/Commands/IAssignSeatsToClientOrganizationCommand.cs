using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;

namespace Bit.Core.Billing.Commands;

public interface IAssignSeatsToClientOrganizationCommand
{
    /// <summary>
    /// Assigns a specified number of <paramref name="seats"/> to a client <paramref name="organization"/> on behalf of
    /// its <paramref name="provider"/>. Seat adjustments for the client organization may autoscale the provider's Stripe
    /// <see cref="Stripe.Subscription"/> depending on the provider's seat minimum for the client <paramref name="organization"/>'s
    /// <see cref="Organization.PlanType"/>.
    /// </summary>
    /// <param name="provider">The MSP that manages the client <paramref name="organization"/>.</param>
    /// <param name="organization">The client organization whose <see cref="seats"/> you want to update.</param>
    /// <param name="seats">The number of seats to assign to the client organization.</param>
    Task AssignSeatsToClientOrganization(
        Provider provider,
        Organization organization,
        int seats);
}
