using Stripe;

namespace Bit.Billing.Services;

public interface IStripeFacade
{
    Task<Charge> GetCharge(
        string chargeId,
        ChargeGetOptions chargeGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default);

    Task<Customer> GetCustomer(
        string customerId,
        CustomerGetOptions customerGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default);

    Task<Invoice> GetInvoice(
        string invoiceId,
        InvoiceGetOptions invoiceGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default);

    Task<PaymentMethod> GetPaymentMethod(
        string paymentMethodId,
        PaymentMethodGetOptions paymentMethodGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default);

    Task<Subscription> GetSubscription(
        string subscriptionId,
        SubscriptionGetOptions subscriptionGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default);

    Task<Subscription> UpdateSubscription(
        string subscriptionId,
        SubscriptionUpdateOptions subscriptionGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default);
}
