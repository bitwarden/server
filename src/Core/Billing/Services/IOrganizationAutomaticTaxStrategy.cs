#nullable enable
using Stripe;

namespace Bit.Core.Billing.Services;

public interface IOrganizationAutomaticTaxStrategy
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="subscription"></param>
    /// <returns>
    /// Returns <see cref="SubscriptionUpdateOptions" /> if changes are to be applied to the subscription, returns null
    /// otherwise.
    /// </returns>
    Task<SubscriptionUpdateOptions?> GetUpdateOptionsAsync(Subscription subscription);
    Task SetCreateOptionsAsync(SubscriptionCreateOptions options, Customer customer);
    Task SetUpdateOptionsAsync(SubscriptionUpdateOptions options, Subscription subscription);
}
