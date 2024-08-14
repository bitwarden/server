using Stripe;

namespace Bit.Billing.Services;

public interface IStripeEventService
{
    /// <summary>
    /// Extracts the <see cref="Charge"/> object from the Stripe <see cref="Event"/>. When <paramref name="fresh"/> is true,
    /// uses the charge ID extracted from the event to retrieve the most up-to-update charge from Stripe's API
    /// and optionally expands it with the provided <see cref="expand"/> options.
    /// </summary>
    /// <param name="stripeEvent">The Stripe webhook event.</param>
    /// <param name="fresh">Determines whether or not to retrieve a fresh copy of the charge object from Stripe.</param>
    /// <param name="expand">Optionally provided to expand the fresh charge object retrieved from Stripe.</param>
    /// <returns>A Stripe <see cref="Charge"/>.</returns>
    /// <exception cref="Exception">Thrown when the Stripe event does not contain a charge object.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="fresh"/> is true and Stripe's API returns a null charge object.</exception>
    Task<Charge> GetCharge(Event stripeEvent, bool fresh = false, List<string> expand = null);

    /// <summary>
    /// Extracts the <see cref="Customer"/> object from the Stripe <see cref="Event"/>. When <paramref name="fresh"/> is true,
    /// uses the customer ID extracted from the event to retrieve the most up-to-update customer from Stripe's API
    /// and optionally expands it with the provided <see cref="expand"/> options.
    /// </summary>
    /// <param name="stripeEvent">The Stripe webhook event.</param>
    /// <param name="fresh">Determines whether or not to retrieve a fresh copy of the customer object from Stripe.</param>
    /// <param name="expand">Optionally provided to expand the fresh customer object retrieved from Stripe.</param>
    /// <returns>A Stripe <see cref="Customer"/>.</returns>
    /// <exception cref="Exception">Thrown when the Stripe event does not contain a customer object.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="fresh"/> is true and Stripe's API returns a null customer object.</exception>
    Task<Customer> GetCustomer(Event stripeEvent, bool fresh = false, List<string> expand = null);

    /// <summary>
    /// Extracts the <see cref="Invoice"/> object from the Stripe <see cref="Event"/>. When <paramref name="fresh"/> is true,
    /// uses the invoice ID extracted from the event to retrieve the most up-to-update invoice from Stripe's API
    /// and optionally expands it with the provided <see cref="expand"/> options.
    /// </summary>
    /// <param name="stripeEvent">The Stripe webhook event.</param>
    /// <param name="fresh">Determines whether or not to retrieve a fresh copy of the invoice object from Stripe.</param>
    /// <param name="expand">Optionally provided to expand the fresh invoice object retrieved from Stripe.</param>
    /// <returns>A Stripe <see cref="Invoice"/>.</returns>
    /// <exception cref="Exception">Thrown when the Stripe event does not contain an invoice object.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="fresh"/> is true and Stripe's API returns a null invoice object.</exception>
    Task<Invoice> GetInvoice(Event stripeEvent, bool fresh = false, List<string> expand = null);

    /// <summary>
    /// Extracts the <see cref="PaymentMethod"/> object from the Stripe <see cref="Event"/>. When <paramref name="fresh"/> is true,
    /// uses the payment method ID extracted from the event to retrieve the most up-to-update payment method from Stripe's API
    /// and optionally expands it with the provided <see cref="expand"/> options.
    /// </summary>
    /// <param name="stripeEvent">The Stripe webhook event.</param>
    /// <param name="fresh">Determines whether or not to retrieve a fresh copy of the payment method object from Stripe.</param>
    /// <param name="expand">Optionally provided to expand the fresh payment method object retrieved from Stripe.</param>
    /// <returns>A Stripe <see cref="PaymentMethod"/>.</returns>
    /// <exception cref="Exception">Thrown when the Stripe event does not contain an payment method object.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="fresh"/> is true and Stripe's API returns a null payment method object.</exception>
    Task<PaymentMethod> GetPaymentMethod(Event stripeEvent, bool fresh = false, List<string> expand = null);

    /// <summary>
    /// Extracts the <see cref="Subscription"/> object from the Stripe <see cref="Event"/>. When <paramref name="fresh"/> is true,
    /// uses the subscription ID extracted from the event to retrieve the most up-to-update subscription from Stripe's API
    /// and optionally expands it with the provided <see cref="expand"/> options.
    /// </summary>
    /// <param name="stripeEvent">The Stripe webhook event.</param>
    /// <param name="fresh">Determines whether or not to retrieve a fresh copy of the subscription object from Stripe.</param>
    /// <param name="expand">Optionally provided to expand the fresh subscription object retrieved from Stripe.</param>
    /// <returns>A Stripe <see cref="Subscription"/>.</returns>
    /// <exception cref="Exception">Thrown when the Stripe event does not contain an subscription object.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="fresh"/> is true and Stripe's API returns a null subscription object.</exception>
    Task<Subscription> GetSubscription(Event stripeEvent, bool fresh = false, List<string> expand = null);

    /// <summary>
    /// Ensures that the customer associated with the Stripe <see cref="Event"/> is in the correct region for this server.
    /// We use the customer instead of the subscription given that all subscriptions have customers, but not all
    /// customers have subscriptions.
    /// </summary>
    /// <param name="stripeEvent">The Stripe webhook event.</param>
    /// <returns>True if the customer's region and the server's region match, otherwise false.</returns>
    Task<bool> ValidateCloudRegion(Event stripeEvent);
}
