using Bit.Billing.Constants;
using Bit.Core;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

/// <summary>
/// Posts non-charge cash application payments applied to subscription invoices. Stripe sends
/// <c>invoice_payment.paid</c> once per payment applied to an invoice; card and bank-debit payments are left to
/// <see cref="ChargeSucceededHandler"/>.
/// </summary>
public class InvoicePaymentPaidHandler(
    Bitwarden.Server.Sdk.Features.IFeatureService featureService,
    IStripeEventService stripeEventService,
    INonChargeCashApplicationRecorder nonChargeCashApplicationRecorder) : IInvoicePaymentPaidHandler
{
    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.InvoicePaymentPaid"/> event type from Stripe.
    /// Stripe and database failures are not caught, so the webhook fails and Stripe retries the event.
    /// </summary>
    public async Task HandleAsync(Event parsedEvent)
    {
        // Released dark: while the flag is off the handler does nothing beyond the standard cloud-region check.
        if (!featureService.IsEnabled(FeatureFlagKeys.PM46970_PostStripeCashApplicationTransactions))
        {
            return;
        }

        // Most invoice payments are card charges; the event payload says so without another Stripe call.
        var eventInvoicePayment = await stripeEventService.GetInvoicePayment(parsedEvent);
        if (eventInvoicePayment.Payment?.Type != NonChargeCashApplicationRecorder.PaymentRecordPaymentType)
        {
            return;
        }

        var invoicePayment = await stripeEventService.GetInvoicePayment(parsedEvent, true, ["invoice"]);
        await nonChargeCashApplicationRecorder.RecordAsync(invoicePayment);
    }
}
