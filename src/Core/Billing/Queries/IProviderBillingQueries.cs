using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Models;
using Bit.Core.Enums;

namespace Bit.Core.Billing.Queries;

public interface IProviderBillingQueries
{
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
