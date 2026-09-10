using Bit.Core.Billing.Services;
using Stripe;

namespace Bit.Invoicing.InvoicePreviews.Stripe;

internal sealed class InvoicePreviewClient(IStripeAdapter stripeAdapter) : IInvoicePreviewClient
{
    public async Task<Invoice> GetInvoiceForPreviewAsync(InvoiceCreatePreviewOptions options)
    {
        options.Expand =
        [
            "lines.data.pricing.price_details.price",
            "total_discount_amounts.discount.source.coupon",
        ];

        var invoice = await stripeAdapter.CreateInvoicePreviewAsync(options);

        // Stripe caps the preview's line sublist at 10 with has_more; fetch the rest so the builder sees every line.
        if (invoice.Lines?.HasMore == true)
        {
            invoice.Lines.Data = await stripeAdapter.ListInvoiceLineItemsAsync(
                invoice.Id,
                new InvoiceLineItemListOptions { Expand = ["data.pricing.price_details.price"] });
            invoice.Lines.HasMore = false;
        }

        return invoice;
    }
}
