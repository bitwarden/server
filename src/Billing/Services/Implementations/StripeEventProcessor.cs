using Bit.Billing.Constants;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class StripeEventProcessor : IStripeEventProcessor
{
    private readonly ILogger<StripeEventProcessor> _logger;
    private readonly ISubscriptionDeletedHandler _subscriptionDeletedHandler;
    private readonly ISubscriptionUpdatedHandler _subscriptionUpdatedHandler;
    private readonly IUpcomingInvoiceHandler _upcomingInvoiceHandler;
    private readonly IChargeSucceededHandler _chargeSucceededHandler;
    private readonly IChargeRefundedHandler _chargeRefundedHandler;
    private readonly IPaymentSucceededHandler _paymentSucceededHandler;
    private readonly IPaymentFailedHandler _paymentFailedHandler;
    private readonly IInvoiceCreatedHandler _invoiceCreatedHandler;
    private readonly IPaymentMethodAttachedHandler _paymentMethodAttachedHandler;
    private readonly ICustomerUpdatedHandler _customerUpdatedHandler;
    private readonly IInvoiceFinalizedHandler _invoiceFinalizedHandler;

    public StripeEventProcessor(
        ILogger<StripeEventProcessor> logger,
        ISubscriptionDeletedHandler subscriptionDeletedHandler,
        ISubscriptionUpdatedHandler subscriptionUpdatedHandler,
        IUpcomingInvoiceHandler upcomingInvoiceHandler,
        IChargeSucceededHandler chargeSucceededHandler,
        IChargeRefundedHandler chargeRefundedHandler,
        IPaymentSucceededHandler paymentSucceededHandler,
        IPaymentFailedHandler paymentFailedHandler,
        IInvoiceCreatedHandler invoiceCreatedHandler,
        IPaymentMethodAttachedHandler paymentMethodAttachedHandler,
        ICustomerUpdatedHandler customerUpdatedHandler,
        IInvoiceFinalizedHandler invoiceFinalizedHandler
    )
    {
        _logger = logger;
        _subscriptionDeletedHandler = subscriptionDeletedHandler;
        _subscriptionUpdatedHandler = subscriptionUpdatedHandler;
        _upcomingInvoiceHandler = upcomingInvoiceHandler;
        _chargeSucceededHandler = chargeSucceededHandler;
        _chargeRefundedHandler = chargeRefundedHandler;
        _paymentSucceededHandler = paymentSucceededHandler;
        _paymentFailedHandler = paymentFailedHandler;
        _invoiceCreatedHandler = invoiceCreatedHandler;
        _paymentMethodAttachedHandler = paymentMethodAttachedHandler;
        _customerUpdatedHandler = customerUpdatedHandler;
        _invoiceFinalizedHandler = invoiceFinalizedHandler;
    }

    public async Task ProcessEventAsync(Event parsedEvent)
    {
        switch (parsedEvent.Type)
        {
            case HandledStripeWebhook.SubscriptionDeleted:
                await _subscriptionDeletedHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.SubscriptionUpdated:
                await _subscriptionUpdatedHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.UpcomingInvoice:
                await _upcomingInvoiceHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.ChargeSucceeded:
                await _chargeSucceededHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.ChargeRefunded:
                await _chargeRefundedHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.PaymentSucceeded:
                await _paymentSucceededHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.PaymentFailed:
                await _paymentFailedHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.InvoiceCreated:
                await _invoiceCreatedHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.PaymentMethodAttached:
                await _paymentMethodAttachedHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.CustomerUpdated:
                await _customerUpdatedHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.InvoiceFinalized:
                await _invoiceFinalizedHandler.HandleAsync(parsedEvent);
                break;
            default:
                _logger.LogWarning("Unsupported event received. {EventType}", parsedEvent.Type);
                break;
        }
    }
}
