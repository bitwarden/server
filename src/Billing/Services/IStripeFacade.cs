using Stripe;

namespace Bit.Billing.Services;

public interface IStripeFacade
{
    Task<Charge> GetCharge(
        string chargeId,
        ChargeGetOptions chargeGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<Customer> GetCustomer(
        string customerId,
        CustomerGetOptions customerGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<Event> GetEvent(
        string eventId,
        EventGetOptions eventGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<Invoice> GetInvoice(
        string invoiceId,
        InvoiceGetOptions invoiceGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<StripeList<Invoice>> ListInvoices(
        InvoiceListOptions options = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<Invoice> UpdateInvoice(
        string invoiceId,
        InvoiceUpdateOptions invoiceGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<Invoice> PayInvoice(
        string invoiceId,
        InvoicePayOptions options = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<Invoice> VoidInvoice(
        string invoiceId,
        InvoiceVoidOptions options = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<PaymentMethod> GetPaymentMethod(
        string paymentMethodId,
        PaymentMethodGetOptions paymentMethodGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<StripeList<Subscription>> ListSubscriptions(
        SubscriptionListOptions options = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<Subscription> GetSubscription(
        string subscriptionId,
        SubscriptionGetOptions subscriptionGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<Subscription> UpdateSubscription(
        string subscriptionId,
        SubscriptionUpdateOptions subscriptionGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<Subscription> CancelSubscription(
        string subscriptionId,
        SubscriptionCancelOptions options = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<TaxRate> GetTaxRate(
        string taxRateId,
        TaxRateGetOptions options = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<Discount> DeleteCustomerDiscount(
        string customerId,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );

    Task<Discount> DeleteSubscriptionDiscount(
        string subscriptionId,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default
    );
}
