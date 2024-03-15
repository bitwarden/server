using Bit.Core.Entities;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Stripe;

using static Bit.Core.Billing.Utilities;

namespace Bit.Core.Billing.Queries.Implementations;

public class SubscriberQueries(
    ILogger<SubscriberQueries> logger,
    IStripeAdapter stripeAdapter) : ISubscriberQueries
{
    public async Task<Subscription> GetSubscriptionOrThrow(ISubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        if (string.IsNullOrEmpty(subscriber.GatewaySubscriptionId))
        {
            logger.LogError("Cannot cancel subscription for subscriber ({ID}) with no GatewaySubscriptionId.", subscriber.Id);

            throw ContactSupport();
        }

        var subscription = await stripeAdapter.SubscriptionGetAsync(subscriber.GatewaySubscriptionId);

        if (subscription != null)
        {
            return subscription;
        }

        logger.LogError("Could not find Stripe subscription ({ID}) to cancel.", subscriber.GatewaySubscriptionId);

        throw ContactSupport();
    }
}
