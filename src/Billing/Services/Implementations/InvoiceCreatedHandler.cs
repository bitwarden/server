using Bit.Billing.Constants;
using Stripe;

namespace Bit.Billing.Services.Implementations;

public class InvoiceCreatedHandler(
    ILogger<InvoiceCreatedHandler> logger,
    IStripeEventService stripeEventService,
    IStripeEventUtilityService stripeEventUtilityService,
    IProviderEventService providerEventService)
    : IInvoiceCreatedHandler
{
    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.InvoiceCreated"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        try
        {
            var invoice = await stripeEventService.GetInvoice(parsedEvent, true, ["customer"]);

            var usingPayPal = invoice.Customer?.Metadata.ContainsKey("btCustomerId") ?? false;

            if (usingPayPal && invoice is
                {
                    AmountDue: > 0,
                    Paid: false,
                    CollectionMethod: "charge_automatically",
                    BillingReason:
                        "subscription_create" or
                        "subscription_cycle" or
                        "automatic_pending_invoice_item_invoice",
                    SubscriptionId: not null and not ""
                })
            {
                await stripeEventUtilityService.AttemptToPayInvoiceAsync(invoice);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to attempt paying for invoice while handling 'invoice.created' event");
        }

        try
        {
            await providerEventService.TryRecordInvoiceLineItems(parsedEvent);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to record provider invoice line items while handling 'invoice.created' event");
        }
    }
}
