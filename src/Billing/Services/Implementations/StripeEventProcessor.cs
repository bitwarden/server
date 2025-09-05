using Bit.Billing.Constants;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class StripeEventProcessor(
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
    IInvoiceFinalizedHandler invoiceFinalizedHandler,
    ISetupIntentSucceededHandler setupIntentSucceededHandler)
    : IStripeEventProcessor
{
    public async Task ProcessEventAsync(Event parsedEvent)
    {
        switch (parsedEvent.Type)
        {
            case HandledStripeWebhook.SubscriptionDeleted:
                await subscriptionDeletedHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.SubscriptionUpdated:
                await subscriptionUpdatedHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.UpcomingInvoice:
                await upcomingInvoiceHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.ChargeSucceeded:
                await chargeSucceededHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.ChargeRefunded:
                await chargeRefundedHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.PaymentSucceeded:
                await paymentSucceededHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.PaymentFailed:
                await paymentFailedHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.InvoiceCreated:
                await invoiceCreatedHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.PaymentMethodAttached:
                await paymentMethodAttachedHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.CustomerUpdated:
                await customerUpdatedHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.InvoiceFinalized:
                await invoiceFinalizedHandler.HandleAsync(parsedEvent);
                break;
            case HandledStripeWebhook.SetupIntentSucceeded:
                await setupIntentSucceededHandler.HandleAsync(parsedEvent);
                break;
            default:
                logger.LogWarning("Unsupported event received. {EventType}", parsedEvent.Type);
                break;
        }
    }

}
