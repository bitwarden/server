using Stripe;

namespace Bit.Core.Billing.Services;

public interface IAutomaticTaxStrategy
{
    Task<SubscriptionUpdateOptions> GetUpdateOptionsAsync(Subscription subscription);
    Task SetCreateOptionsAsync(SubscriptionCreateOptions options, Customer customer);
    Task SetUpdateOptionsAsync(SubscriptionUpdateOptions options, Subscription subscription);
}
