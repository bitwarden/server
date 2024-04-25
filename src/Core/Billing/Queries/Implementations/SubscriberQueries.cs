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
    public async Task<Customer> GetCustomer(
        ISubscriber subscriber,
        CustomerGetOptions customerGetOptions = null)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        if (string.IsNullOrEmpty(subscriber.GatewayCustomerId))
        {
            logger.LogError("Cannot retrieve customer for subscriber ({SubscriberID}) with no {FieldName}", subscriber.Id, nameof(subscriber.GatewayCustomerId));

            return null;
        }

        var customer = await stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId, customerGetOptions);

        if (customer != null)
        {
            return customer;
        }

        logger.LogError("Could not find Stripe customer ({CustomerID}) for subscriber ({SubscriberID})", subscriber.GatewayCustomerId, subscriber.Id);

        return null;
    }

    public async Task<Subscription> GetSubscription(
        ISubscriber subscriber,
        SubscriptionGetOptions subscriptionGetOptions = null)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        if (string.IsNullOrEmpty(subscriber.GatewaySubscriptionId))
        {
            logger.LogError("Cannot retrieve subscription for subscriber ({SubscriberID}) with no {FieldName}", subscriber.Id, nameof(subscriber.GatewaySubscriptionId));

            return null;
        }

        var subscription = await stripeAdapter.SubscriptionGetAsync(subscriber.GatewaySubscriptionId, subscriptionGetOptions);

        if (subscription != null)
        {
            return subscription;
        }

        logger.LogError("Could not find Stripe subscription ({SubscriptionID}) for subscriber ({SubscriberID})", subscriber.GatewaySubscriptionId, subscriber.Id);

        return null;
    }

    public async Task<Subscription> GetSubscriptionOrThrow(
        ISubscriber subscriber,
        SubscriptionGetOptions subscriptionGetOptions = null)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        if (string.IsNullOrEmpty(subscriber.GatewaySubscriptionId))
        {
            logger.LogError("Cannot retrieve subscription for subscriber ({SubscriberID}) with no {FieldName}", subscriber.Id, nameof(subscriber.GatewaySubscriptionId));

            throw ContactSupport();
        }

        var subscription = await stripeAdapter.SubscriptionGetAsync(subscriber.GatewaySubscriptionId, subscriptionGetOptions);

        if (subscription != null)
        {
            return subscription;
        }

        logger.LogError("Could not find Stripe subscription ({SubscriptionID}) for subscriber ({SubscriberID})", subscriber.GatewaySubscriptionId, subscriber.Id);

        throw ContactSupport();
    }

    public async Task<Customer> GetCustomerOrThrow(
        ISubscriber subscriber,
        CustomerGetOptions customerGetOptions = null)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        if (string.IsNullOrEmpty(subscriber.GatewayCustomerId))
        {
            logger.LogError("Cannot retrieve customer for subscriber ({SubscriberID}) with no {FieldName}", subscriber.Id, nameof(subscriber.GatewayCustomerId));

            throw ContactSupport();
        }

        var customer = await stripeAdapter.CustomerGetAsync(subscriber.GatewayCustomerId, customerGetOptions);

        if (customer != null)
        {
            return customer;
        }

        logger.LogError("Could not find Stripe customer ({CustomerID}) for subscriber ({SubscriberID})", subscriber.GatewayCustomerId, subscriber.Id);

        throw ContactSupport();
    }
}
