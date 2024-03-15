using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Repositories;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Queries.Implementations;

public class ProviderBillingQueries(
    ILogger<ProviderBillingQueries> logger,
    IProviderPlanRepository providerPlanRepository,
    IProviderRepository providerRepository,
    ISubscriberQueries subscriberQueries) : IProviderBillingQueries
{
    public async Task<ProviderSubscriptionData> GetSubscriptionData(Guid providerId)
    {
        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            logger.LogError(
                "Could not find provider ({ID}) when retrieving subscription data.",
                providerId);

            return null;
        }

        var subscription = await subscriberQueries.GetSubscription(provider, new SubscriptionGetOptions
        {
            Expand = ["customer"]
        });

        if (subscription == null)
        {
            return null;
        }

        var providerPlans = await providerPlanRepository.GetByProviderId(providerId);

        var configuredProviderPlans = providerPlans
            .Where(providerPlan => providerPlan.Configured)
            .Select(ConfiguredProviderPlan.From)
            .ToList();

        return new ProviderSubscriptionData(
            configuredProviderPlans,
            subscription);
    }
}
