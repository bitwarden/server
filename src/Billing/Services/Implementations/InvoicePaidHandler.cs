using Bit.Billing.Constants;
using Bit.Core;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

/// <summary>
/// Error handling for invoices without a subscription link. Bitwarden bills only through subscriptions, but a
/// subscription invoice can lose its subscription link, for example through a manually added pending invoice item.
/// Stripe then sends no <c>invoice_payment.paid</c> for its payments, only <c>invoice.paid</c>. Payments are keyed on
/// the invoice payment ID, so a payment seen through both events is posted once.
/// </summary>
public class InvoicePaidHandler(
    Bitwarden.Server.Sdk.Features.IFeatureService featureService,
    IStripeEventService stripeEventService,
    IStripeFacade stripeFacade,
    INonChargeCashApplicationRecorder nonChargeCashApplicationRecorder) : IInvoicePaidHandler
{
    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.InvoicePaid"/> event type from Stripe.
    /// Stripe and database failures are not caught, so the webhook fails and Stripe retries the event.
    /// </summary>
    public async Task HandleAsync(Event parsedEvent)
    {
        // Released dark: while the flag is off the handler does nothing beyond the standard cloud-region check.
        if (!featureService.IsEnabled(FeatureFlagKeys.PM46970_PostStripeCashApplicationTransactions))
        {
            return;
        }

        // Subscription invoices are handled through invoice_payment.paid, and credit-only invoices have no new money;
        // the event payload says so without another Stripe call.
        var eventInvoice = await stripeEventService.GetInvoice(parsedEvent);
        if (eventInvoice.Parent?.SubscriptionDetails != null || eventInvoice.AmountPaid <= 0)
        {
            return;
        }

        var invoice = await stripeEventService.GetInvoice(parsedEvent, true, ["payments"]);

        var invoicePayments = invoice.Payments?.Data ?? [];
        if (invoice.Payments?.HasMore == true)
        {
            invoicePayments = [];
            await foreach (var invoicePayment in stripeFacade.ListInvoicePaymentsAutoPagingAsync(
                               new InvoicePaymentListOptions { Invoice = invoice.Id }))
            {
                invoicePayments.Add(invoicePayment);
            }
        }

        foreach (var invoicePayment in invoicePayments)
        {
            invoicePayment.Invoice = invoice;
            await nonChargeCashApplicationRecorder.RecordAsync(invoicePayment);
        }
    }
}
