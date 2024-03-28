using Bit.Core.Billing.Models;

namespace Bit.Core.Billing.Queries;

public interface IProviderBillingQueries
{
    /// <summary>
    /// Retrieves a provider's billing subscription data.
    /// </summary>
    /// <param name="providerId">The ID of the provider to retrieve subscription data for.</param>
    /// <returns>A <see cref="ProviderSubscriptionData"/> object containing the provider's Stripe <see cref="Stripe.Subscription"/> and their <see cref="ConfiguredProviderPlan"/>s.</returns>
    /// <remarks>This method opts for returning <see langword="null"/> rather than throwing exceptions, making it ideal for surfacing data from API endpoints.</remarks>
    Task<ProviderSubscriptionData> GetSubscriptionData(Guid providerId);
}
