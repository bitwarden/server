#nullable enable
using Stripe;

namespace Bit.Core.Billing.Services;

public interface IOrganizationAutomaticTaxStrategy
{
    Task<SubscriptionUpdateOptions?> GetUpdateOptionsAsync(Subscription subscription);
    Task SetCreateOptionsAsync(SubscriptionCreateOptions options, Customer customer);
    Task SetUpdateOptionsAsync(SubscriptionUpdateOptions options, Subscription subscription);
}
