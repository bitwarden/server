#nullable enable
using Stripe;

namespace Bit.Core.Billing.Services;

public interface IIndividualAutomaticTaxStrategy
{
    SubscriptionUpdateOptions? GetUpdateOptions(Subscription subscription);
    void SetCreateOptions(SubscriptionCreateOptions options, Customer customer);
    void SetUpdateOptions(SubscriptionUpdateOptions options, Subscription subscription);
}
