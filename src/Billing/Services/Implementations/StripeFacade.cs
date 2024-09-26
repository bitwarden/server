using Stripe;

namespace Bit.Billing.Services.Implementations;

public class StripeFacade : IStripeFacade
{
    private readonly ChargeService _chargeService = new();
    private readonly CustomerService _customerService = new();
    private readonly EventService _eventService = new();
    private readonly InvoiceService _invoiceService = new();
    private readonly PaymentMethodService _paymentMethodService = new();
    private readonly SubscriptionService _subscriptionService = new();
    private readonly TaxRateService _taxRateService = new();
    private readonly DiscountService _discountService = new();

    public async Task<Charge> GetCharge(
        string chargeId,
        ChargeGetOptions chargeGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _chargeService.GetAsync(chargeId, chargeGetOptions, requestOptions, cancellationToken);

    public async Task<Event> GetEvent(
        string eventId,
        EventGetOptions eventGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _eventService.GetAsync(eventId, eventGetOptions, requestOptions, cancellationToken);

    public async Task<Customer> GetCustomer(
        string customerId,
        CustomerGetOptions customerGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _customerService.GetAsync(customerId, customerGetOptions, requestOptions, cancellationToken);

    public async Task<Invoice> GetInvoice(
        string invoiceId,
        InvoiceGetOptions invoiceGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _invoiceService.GetAsync(invoiceId, invoiceGetOptions, requestOptions, cancellationToken);

    public async Task<StripeList<Invoice>> ListInvoices(
        InvoiceListOptions options = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _invoiceService.ListAsync(options, requestOptions, cancellationToken);

    public async Task<Invoice> UpdateInvoice(
        string invoiceId,
        InvoiceUpdateOptions invoiceGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _invoiceService.UpdateAsync(invoiceId, invoiceGetOptions, requestOptions, cancellationToken);

    public async Task<Invoice> PayInvoice(string invoiceId, InvoicePayOptions options = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _invoiceService.PayAsync(invoiceId, options, requestOptions, cancellationToken);

    public async Task<Invoice> VoidInvoice(
        string invoiceId,
        InvoiceVoidOptions options = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _invoiceService.VoidInvoiceAsync(invoiceId, options, requestOptions, cancellationToken);

    public async Task<PaymentMethod> GetPaymentMethod(
        string paymentMethodId,
        PaymentMethodGetOptions paymentMethodGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _paymentMethodService.GetAsync(paymentMethodId, paymentMethodGetOptions, requestOptions, cancellationToken);

    public async Task<StripeList<Subscription>> ListSubscriptions(SubscriptionListOptions options = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _subscriptionService.ListAsync(options, requestOptions, cancellationToken);

    public async Task<Subscription> GetSubscription(
        string subscriptionId,
        SubscriptionGetOptions subscriptionGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _subscriptionService.GetAsync(subscriptionId, subscriptionGetOptions, requestOptions, cancellationToken);

    public async Task<Subscription> UpdateSubscription(
        string subscriptionId,
        SubscriptionUpdateOptions subscriptionUpdateOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _subscriptionService.UpdateAsync(subscriptionId, subscriptionUpdateOptions, requestOptions, cancellationToken);

    public async Task<Subscription> CancelSubscription(
        string subscriptionId,
        SubscriptionCancelOptions options = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _subscriptionService.CancelAsync(subscriptionId, options, requestOptions, cancellationToken);

    public async Task<TaxRate> GetTaxRate(
        string taxRateId,
        TaxRateGetOptions options = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _taxRateService.GetAsync(taxRateId, options, requestOptions, cancellationToken);

    public async Task<Discount> DeleteCustomerDiscount(
        string customerId,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _discountService.DeleteCustomerDiscountAsync(customerId, requestOptions, cancellationToken);

    public async Task<Discount> DeleteSubscriptionDiscount(
        string subscriptionId,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _discountService.DeleteSubscriptionDiscountAsync(subscriptionId, requestOptions, cancellationToken);
}
