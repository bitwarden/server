using Bit.Core.Billing.Services;
using Stripe;

namespace Bit.Invoicing.InvoicePreviews.Stripe;

internal sealed class InvoicePreviewClient(IStripeAdapter stripeAdapter) : IInvoicePreviewClient
{
    public Task<Invoice> GetInvoiceForPreviewAsync(InvoiceCreatePreviewOptions options)
    {
        options.Expand =
        [
            "lines.data.pricing.price_details.price",
            "total_discount_amounts.discount.source.coupon",
        ];
        return stripeAdapter.CreateInvoicePreviewAsync(options);
    }
}
