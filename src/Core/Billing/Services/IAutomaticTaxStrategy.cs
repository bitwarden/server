#nullable enable
using Stripe;

namespace Bit.Core.Billing.Services;

public interface IAutomaticTaxStrategy
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="subscription"></param>
    /// <returns>
    /// Returns <see cref="SubscriptionUpdateOptions" /> if changes are to be applied to the subscription, returns null
    /// otherwise.
    /// </returns>
    SubscriptionUpdateOptions? GetUpdateOptions(Subscription subscription);
    void SetCreateOptions(SubscriptionCreateOptions options, Customer customer);
    void SetUpdateOptions(SubscriptionUpdateOptions options, Subscription subscription);
}
