using Stripe;

namespace Bit.Core.Models.BitStripe;

/// <summary>
/// A model derived from the Stripe <see cref="InvoiceListOptions"/> class that includes a flag used to
/// retrieve all invoices from the Stripe API rather than a limited set.
/// </summary>
public class StripeInvoiceListOptions : InvoiceListOptions
{
    public bool SelectAll { get; set; }

    public InvoiceListOptions ToInvoiceListOptions()
    {
        var options = (InvoiceListOptions)this;

        if (!SelectAll)
        {
            return options;
        }

        options.EndingBefore = null;
        options.StartingAfter = null;

        return options;
    }
}
