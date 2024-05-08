using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models;
using Bit.Core.Entities;
using Stripe;

namespace Bit.Core.Billing.Services;

public interface ISubscriberService
{
    /// <summary>
    /// Cancels a suscriber's subscription while including user-provided feedback via the <paramref name="offboardingSurveyResponse"/>.
    /// If the <paramref name="cancelImmediately"/> flag is <see langword="false"/>,
    /// this command sets the subscription's <b>"cancel_at_end_of_period"</b> property to <see langword="true"/>.
    /// Otherwise, this command cancels the subscription immediately.
    /// </summary>
    /// <param name="subscriber">The subscriber with the subscription to cancel.</param>
    /// <param name="offboardingSurveyResponse">An <see cref="OffboardingSurveyResponse"/> DTO containing user-provided feedback on why they are cancelling the subscription.</param>
    /// <param name="cancelImmediately">A flag indicating whether to cancel the subscription immediately or at the end of the subscription period.</param>
    Task CancelSubscription(
        ISubscriber subscriber,
        OffboardingSurveyResponse offboardingSurveyResponse,
        bool cancelImmediately);

    /// <summary>
    /// Retrieves a Stripe <see cref="Customer"/> using the <paramref name="subscriber"/>'s <see cref="ISubscriber.GatewayCustomerId"/> property.
    /// </summary>
    /// <param name="subscriber">The subscriber to retrieve the Stripe customer for.</param>
    /// <param name="customerGetOptions">Optional parameters that can be passed to Stripe to expand or modify the customer.</param>
    /// <returns>A Stripe <see cref="Customer"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="subscriber"/> is <see langword="null"/>.</exception>
    /// <remarks>This method opts for returning <see langword="null"/> rather than throwing exceptions, making it ideal for surfacing data from API endpoints.</remarks>
    Task<Customer> GetCustomer(
        ISubscriber subscriber,
        CustomerGetOptions customerGetOptions = null);

    /// <summary>
    /// Retrieves a Stripe <see cref="Customer"/> using the <paramref name="subscriber"/>'s <see cref="ISubscriber.GatewayCustomerId"/> property.
    /// </summary>
    /// <param name="subscriber">The subscriber to retrieve the Stripe customer for.</param>
    /// <param name="customerGetOptions">Optional parameters that can be passed to Stripe to expand or modify the customer.</param>
    /// <returns>A Stripe <see cref="Customer"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="subscriber"/> is <see langword="null"/>.</exception>
    /// <exception cref="BillingException">Thrown when the subscriber's <see cref="ISubscriber.GatewayCustomerId"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="BillingException">Thrown when the <see cref="Customer"/> returned from Stripe's API is null.</exception>
    Task<Customer> GetCustomerOrThrow(
        ISubscriber subscriber,
        CustomerGetOptions customerGetOptions = null);

    /// <summary>
    /// Retrieves a Stripe <see cref="Subscription"/> using the <paramref name="subscriber"/>'s <see cref="ISubscriber.GatewaySubscriptionId"/> property.
    /// </summary>
    /// <param name="subscriber">The subscriber to retrieve the Stripe subscription for.</param>
    /// <param name="subscriptionGetOptions">Optional parameters that can be passed to Stripe to expand or modify the subscription.</param>
    /// <returns>A Stripe <see cref="Subscription"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="subscriber"/> is <see langword="null"/>.</exception>
    /// <remarks>This method opts for returning <see langword="null"/> rather than throwing exceptions, making it ideal for surfacing data from API endpoints.</remarks>
    Task<Subscription> GetSubscription(
        ISubscriber subscriber,
        SubscriptionGetOptions subscriptionGetOptions = null);

    /// <summary>
    /// Retrieves a Stripe <see cref="Subscription"/> using the <paramref name="subscriber"/>'s <see cref="ISubscriber.GatewaySubscriptionId"/> property.
    /// </summary>
    /// <param name="subscriber">The subscriber to retrieve the Stripe subscription for.</param>
    /// <param name="subscriptionGetOptions">Optional parameters that can be passed to Stripe to expand or modify the subscription.</param>
    /// <returns>A Stripe <see cref="Subscription"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="subscriber"/> is <see langword="null"/>.</exception>
    /// <exception cref="BillingException">Thrown when the subscriber's <see cref="ISubscriber.GatewaySubscriptionId"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="BillingException">Thrown when the <see cref="Subscription"/> returned from Stripe's API is null.</exception>
    Task<Subscription> GetSubscriptionOrThrow(
        ISubscriber subscriber,
        SubscriptionGetOptions subscriptionGetOptions = null);
}
