using Stripe;

namespace Bit.Billing.Services;

/// <summary>
/// Posts non-charge cash application payments (checks, wires and bank transfers applied to a Stripe invoice as a
/// Payment Record, with no Stripe charge) as Bitwarden transactions.
/// </summary>
public interface INonChargeCashApplicationRecorder
{
    /// <summary>
    /// Posts the invoice payment if it is a paid non-charge cash application payment; otherwise does nothing.
    /// Safe to call more than once for the same payment.
    /// </summary>
    /// <param name="invoicePayment">The invoice payment, with its <see cref="InvoicePayment.Invoice"/> expanded.</param>
    Task RecordAsync(InvoicePayment invoicePayment);
}
