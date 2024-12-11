using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class InvoiceCreatedHandler(
    ILogger<InvoiceCreatedHandler> logger,
    IStripeEventService stripeEventService,
    IStripeEventUtilityService stripeEventUtilityService,
    IProviderEventService providerEventService
) : IInvoiceCreatedHandler
{
    /// <summary>
    /// <para>
    /// This handler processes the `invoice.created` event in <see href="https://docs.stripe.com/api/events/types#event_types-invoice.created">Stripe</see>. It has
    /// two primary responsibilities.
    /// </para>
    /// <para>
    /// 1. Checks to see if the newly created invoice belongs to a PayPal customer. If it does, and the invoice is ready to be paid, it will attempt to pay the invoice
    /// with Braintree and then let Stripe know the invoice can be marked as paid.
    /// </para>
    /// <para>
    /// 2. If the invoice is for a provider, it records a point-in-time snapshot of the invoice broken down by the provider's client organizations. This is later used in
    /// the provider invoice export.
    /// </para>
    /// </summary>
    public async Task HandleAsync(Event parsedEvent)
    {
        try
        {
            var invoice = await stripeEventService.GetInvoice(parsedEvent, true, ["customer"]);

            var usingPayPal = invoice.Customer?.Metadata.ContainsKey("btCustomerId") ?? false;

            if (
                usingPayPal
                && invoice
                    is {
                        AmountDue: > 0,
                        Paid: false,
                        CollectionMethod: "charge_automatically",
                        BillingReason: "subscription_create"
                            or "subscription_cycle"
                            or "automatic_pending_invoice_item_invoice",
                        SubscriptionId: not null and not ""
                    }
            )
            {
                await stripeEventUtilityService.AttemptToPayInvoiceAsync(invoice);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to attempt paying for invoice while handling 'invoice.created' event ({EventID})",
                parsedEvent.Id
            );
        }

        try
        {
            await providerEventService.TryRecordInvoiceLineItems(parsedEvent);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to record provider invoice line items while handling 'invoice.created' event ({EventID})",
                parsedEvent.Id
            );
        }
    }
}
