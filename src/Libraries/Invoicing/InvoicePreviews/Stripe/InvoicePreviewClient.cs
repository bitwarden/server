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

        await ExpandCouponsAsync(invoice);

        return invoice;
    }

    // The preview response cannot carry coupon.applies_to (its expand chain ends at coupon, Stripe's 4-level
    // cap), so each distinct coupon is refetched with applies_to expanded and spliced onto its discount.
    private async Task ExpandCouponsAsync(Invoice invoice)
    {
        var amounts = (invoice.TotalDiscountAmounts ?? [])
            .Where(amount => amount.Discount?.Source?.Coupon is not null)
            .ToList();

        foreach (var couponId in amounts.Select(amount => amount.Discount.Source.Coupon.Id).Distinct())
        {
            Coupon? enriched;
            try
            {
                enriched = await stripeAdapter.GetCouponAsync(couponId, new CouponGetOptions { Expand = ["applies_to"] });
            }
            catch (StripeException)
            {
                // The coupon may have been deleted since it was attached; keep the un-enriched coupon.
                continue;
            }

            foreach (var amount in amounts.Where(amount => amount.Discount.Source.Coupon.Id == couponId))
            {
                amount.Discount.Source.Coupon = enriched;
            }
        }
    }
}
