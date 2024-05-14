using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Stripe;

namespace Bit.Core.Billing.Queries;

public interface ISubscriberQueries
{
    /// <summary>
    /// Retrieves a Stripe <see cref="Customer"/> using the <paramref name="subscriber"/>'s <see cref="ISubscriber.GatewayCustomerId"/> property.
    /// </summary>
    /// <param name="subscriber">The organization, provider or user to retrieve the customer for.</param>
    /// <param name="customerGetOptions">Optional parameters that can be passed to Stripe to expand or modify the <see cref="Customer"/>.</param>
    /// <returns>A Stripe <see cref="Customer"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="subscriber"/> is <see langword="null"/>.</exception>
    /// <remarks>This method opts for returning <see langword="null"/> rather than throwing exceptions, making it ideal for surfacing data from API endpoints.</remarks>
    Task<Customer> GetCustomer(
        ISubscriber subscriber,
        CustomerGetOptions customerGetOptions = null);

    /// <summary>
    /// Retrieves a Stripe <see cref="Subscription"/> using the <paramref name="subscriber"/>'s <see cref="ISubscriber.GatewaySubscriptionId"/> property.
    /// </summary>
    /// <param name="subscriber">The organization, provider or user to retrieve the subscription for.</param>
    /// <param name="subscriptionGetOptions">Optional parameters that can be passed to Stripe to expand or modify the <see cref="Subscription"/>.</param>
    /// <returns>A Stripe <see cref="Subscription"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="subscriber"/> is <see langword="null"/>.</exception>
    /// <remarks>This method opts for returning <see langword="null"/> rather than throwing exceptions, making it ideal for surfacing data from API endpoints.</remarks>
    Task<Subscription> GetSubscription(
        ISubscriber subscriber,
        SubscriptionGetOptions subscriptionGetOptions = null);

    /// <summary>
    /// Retrieves a Stripe <see cref="Customer"/> using the <paramref name="subscriber"/>'s <see cref="ISubscriber.GatewayCustomerId"/> property.
    /// </summary>
    /// <param name="subscriber">The organization or user to retrieve the subscription for.</param>
    /// <param name="customerGetOptions">Optional parameters that can be passed to Stripe to expand or modify the <see cref="Customer"/>.</param>
    /// <returns>A Stripe <see cref="Customer"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="subscriber"/> is <see langword="null"/>.</exception>
    /// <exception cref="GatewayException">Thrown when the subscriber's <see cref="ISubscriber.GatewayCustomerId"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="GatewayException">Thrown when the <see cref="Customer"/> returned from Stripe's API is null.</exception>
    Task<Customer> GetCustomerOrThrow(
        ISubscriber subscriber,
        CustomerGetOptions customerGetOptions = null);

    /// <summary>
    /// Retrieves a Stripe <see cref="Subscription"/> using the <paramref name="subscriber"/>'s <see cref="ISubscriber.GatewaySubscriptionId"/> property.
    /// </summary>
    /// <param name="subscriber">The organization or user to retrieve the subscription for.</param>
    /// <param name="subscriptionGetOptions">Optional parameters that can be passed to Stripe to expand or modify the <see cref="Subscription"/>.</param>
    /// <returns>A Stripe <see cref="Subscription"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="subscriber"/> is <see langword="null"/>.</exception>
    /// <exception cref="GatewayException">Thrown when the subscriber's <see cref="ISubscriber.GatewaySubscriptionId"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="GatewayException">Thrown when the <see cref="Subscription"/> returned from Stripe's API is null.</exception>
    Task<Subscription> GetSubscriptionOrThrow(
        ISubscriber subscriber,
        SubscriptionGetOptions subscriptionGetOptions = null);
}
