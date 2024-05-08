using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Models;
using Bit.Core.Enums;

namespace Bit.Core.Billing.Services;

public interface IProviderBillingService
{
    /// <summary>
    /// Assigns a specified number of <paramref name="seats"/> to a client <paramref name="organization"/> on behalf of
    /// its <paramref name="provider"/>. Seat adjustments for the client organization may autoscale the provider's Stripe
    /// <see cref="Stripe.Subscription"/> depending on the provider's seat minimum for the client <paramref name="organization"/>'s
    /// <see cref="PlanType"/>.
    /// </summary>
    /// <param name="provider">The MSP that manages the client <paramref name="organization"/>.</param>
    /// <param name="organization">The client organization whose <see cref="seats"/> you want to update.</param>
    /// <param name="seats">The number of seats to assign to the client organization.</param>
    Task AssignSeatsToClientOrganization(
        Provider provider,
        Organization organization,
        int seats);

    /// <summary>
    /// Retrieves the number of seats an MSP has assigned to its client organizations with a specified <paramref name="planType"/>.
    /// </summary>
    /// <param name="providerId">The ID of the MSP to retrieve the assigned seat total for.</param>
    /// <param name="planType">The type of plan to retrieve the assigned seat total for.</param>
    /// <returns>An <see cref="int"/> representing the number of seats the provider has assigned to its client organizations with the specified <paramref name="planType"/>.</returns>
    /// <exception cref="BillingException">Thrown when the provider represented by the <paramref name="providerId"/> is <see langword="null"/>.</exception>
    /// <exception cref="BillingException">Thrown when the provider represented by the <paramref name="providerId"/> has <see cref="Provider.Type"/> <see cref="ProviderType.Reseller"/>.</exception>
    Task<int> GetAssignedSeatTotalForPlanOrThrow(Guid providerId, PlanType planType);

    /// <summary>
    /// Retrieves a provider's billing subscription data.
    /// </summary>
    /// <param name="providerId">The ID of the provider to retrieve subscription data for.</param>
    /// <returns>A <see cref="ProviderSubscriptionDTO"/> object containing the provider's Stripe <see cref="Stripe.Subscription"/> and their <see cref="ConfiguredProviderPlanDTO"/>s.</returns>
    /// <remarks>This method opts for returning <see langword="null"/> rather than throwing exceptions, making it ideal for surfacing data from API endpoints.</remarks>
    Task<ProviderSubscriptionDTO> GetSubscriptionDTO(Guid providerId);
}
