using Bit.Core.Billing.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Stripe;
using PaymentMethod = Bit.Core.Billing.Models.PaymentMethod;

namespace Bit.Core.Billing.Services;

public interface ISubscriberService
{
    /// <summary>
    /// Cancels a subscriber's subscription while including user-provided feedback via the <paramref name="offboardingSurveyResponse"/>.
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
    /// Creates a Braintree <see cref="Braintree.Customer"/> for the provided <paramref name="subscriber"/> while attaching the provided <paramref name="paymentMethodNonce"/>.
    /// </summary>
    /// <param name="subscriber">The subscriber to create a Braintree customer for.</param>
    /// <param name="paymentMethodNonce">A nonce representing the PayPal payment method the customer will use for payments.</param>
    /// <returns>The <see cref="Braintree.Customer.Id"/> of the created Braintree customer.</returns>
    Task<string> CreateBraintreeCustomer(
        ISubscriber subscriber,
        string paymentMethodNonce);

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
    /// Retrieves the account credit, a masked representation of the default payment source and the tax information for the
    /// provided <paramref name="subscriber"/>. This is essentially a consolidated invocation of the <see cref="GetPaymentSource"/>
    /// and <see cref="GetTaxInformation"/> methods with a response that includes the customer's <see cref="Stripe.Customer.Balance"/> as account credit in order to cut down on Stripe API calls.
    /// </summary>
    /// <param name="subscriber">The subscriber to retrieve payment method for.</param>
    /// <returns>A <see cref="Models.PaymentMethod"/> containing the subscriber's account credit, payment source and tax information.</returns>
    Task<PaymentMethod> GetPaymentMethod(
        ISubscriber subscriber);

    /// <summary>
    /// Retrieves a masked representation of the subscriber's payment source for presentation to a client.
    /// </summary>
    /// <param name="subscriber">The subscriber to retrieve the payment source for.</param>
    /// <returns>A <see cref="PaymentSource"/> containing a non-identifiable description of the subscriber's payment source. Example: VISA, *4242, 10/2026</returns>
    Task<PaymentSource> GetPaymentSource(
        ISubscriber subscriber);

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

    /// <summary>
    /// Retrieves the <paramref name="subscriber"/>'s tax information using their Stripe <see cref="Stripe.Customer"/>'s <see cref="Stripe.Customer.Address"/>.
    /// </summary>
    /// <param name="subscriber">The subscriber to retrieve the tax information for.</param>
    /// <returns>A <see cref="TaxInformation"/> representing the <paramref name="subscriber"/>'s tax information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="subscriber"/> is <see langword="null"/>.</exception>
    /// <remarks>This method opts for returning <see langword="null"/> rather than throwing exceptions, making it ideal for surfacing data from API endpoints.</remarks>
    Task<TaxInformation> GetTaxInformation(
        ISubscriber subscriber);

    /// <summary>
    /// Attempts to remove a subscriber's saved payment source. If the Stripe <see cref="Stripe.Customer"/> representing the
    /// <paramref name="subscriber"/> contains a valid <b>"btCustomerId"</b> key in its <see cref="Stripe.Customer.Metadata"/> property,
    /// this command will attempt to remove the Braintree <see cref="Braintree.PaymentMethod"/>. Otherwise, it will attempt to remove the
    /// Stripe <see cref="Stripe.PaymentMethod"/>.
    /// </summary>
    /// <param name="subscriber">The subscriber to remove the saved payment source for.</param>
    Task RemovePaymentSource(ISubscriber subscriber);

    /// <summary>
    /// Updates the payment source for the provided <paramref name="subscriber"/> using the <paramref name="tokenizedPaymentSource"/>.
    /// The following types are supported: [<see cref="PaymentMethodType.Card"/>, <see cref="PaymentMethodType.BankAccount"/>, <see cref="PaymentMethodType.PayPal"/>].
    /// For each type, updating the payment source will attempt to establish a new payment source using the token in the <see cref="TokenizedPaymentSource"/>. Then, it will
    /// remove the exising payment source(s) linked to the subscriber's customer.
    /// </summary>
    /// <param name="subscriber">The subscriber to update the payment method for.</param>
    /// <param name="tokenizedPaymentSource">A DTO representing a tokenized payment method.</param>
    Task UpdatePaymentSource(
        ISubscriber subscriber,
        TokenizedPaymentSource tokenizedPaymentSource);

    /// <summary>
    /// Updates the tax information for the provided <paramref name="subscriber"/>.
    /// </summary>
    /// <param name="subscriber">The <paramref name="subscriber"/> to update the tax information for.</param>
    /// <param name="taxInformation">A <see cref="TaxInformation"/> representing the <paramref name="subscriber"/>'s updated tax information.</param>
    Task UpdateTaxInformation(
        ISubscriber subscriber,
        TaxInformation taxInformation);

    /// <summary>
    /// Verifies the subscriber's pending bank account using the provided <paramref name="descriptorCode"/>.
    /// </summary>
    /// <param name="subscriber">The subscriber to verify the bank account for.</param>
    /// <param name="descriptorCode">The code attached to a deposit made to the subscriber's bank account in order to ensure they have access to it.
    /// <a href="https://docs.stripe.com/payments/ach-debit/set-up-payment">Learn more.</a></param>
    /// <returns></returns>
    Task VerifyBankAccount(
        ISubscriber subscriber,
        string descriptorCode);
}
