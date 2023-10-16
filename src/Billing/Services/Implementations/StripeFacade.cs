using Stripe;

namespace Bit.Billing.Services.Implementations;

public class StripeFacade : IStripeFacade
{
    private readonly ChargeService _chargeService = new();
    private readonly CustomerService _customerService = new();
    private readonly InvoiceService _invoiceService = new();
    private readonly PaymentMethodService _paymentMethodService = new();
    private readonly SubscriptionService _subscriptionService = new();

    public async Task<Charge> GetCharge(
        string chargeId,
        ChargeGetOptions chargeGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _chargeService.GetAsync(chargeId, chargeGetOptions, requestOptions, cancellationToken);

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

    public async Task<PaymentMethod> GetPaymentMethod(
        string paymentMethodId,
        PaymentMethodGetOptions paymentMethodGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _paymentMethodService.GetAsync(paymentMethodId, paymentMethodGetOptions, requestOptions, cancellationToken);

    public async Task<Subscription> GetSubscription(
        string subscriptionId,
        SubscriptionGetOptions subscriptionGetOptions = null,
        RequestOptions requestOptions = null,
        CancellationToken cancellationToken = default) =>
        await _subscriptionService.GetAsync(subscriptionId, subscriptionGetOptions, requestOptions, cancellationToken);
}
