using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Invoicing.InvoicePreviews.Models;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Invoicing.InvoicePreviews;

internal sealed record PartitionedDiscounts(
    InvoicePreviewDiscount[] CartLevel,
    IReadOnlyDictionary<string, InvoicePreviewDiscount[]> ItemLevel);

/// <summary>Classifies each invoice discount by scope. Coupons are resolved once via the top-level total_discount_amounts expansion and matched onto lines by DiscountId.</summary>
internal static class DiscountMapper
{
    internal static PartitionedDiscounts Partition(Invoice invoice, ILogger logger)
    {
        var resolved = new Dictionary<string, ResolvedDiscount>();
        foreach (var total in invoice.TotalDiscountAmounts ?? [])
        {
            // Log, don't drop: a lost coupon expansion would otherwise erase every discount unseen.
            if (total.Discount?.Source?.Coupon is not { } coupon)
            {
                logger.LogError("Discount {DiscountId} has no expanded coupon; dropped.", total.DiscountId);
                continue;
            }
            resolved.Add(total.DiscountId, new ResolvedDiscount(coupon, total.Amount / 100m));
        }

        var cartLevel = resolved.Values.Where(discount => !discount.IsItemScoped)
            .Select(discount => discount.ToPreviewDiscount(discount.AggregateAmount)).ToArray();

        var itemLevel = new Dictionary<string, InvoicePreviewDiscount[]>();
        var attached = new HashSet<string>();
        foreach (var line in invoice.Lines?.Data ?? [])
        {
            var reference = line.Pricing?.PriceDetails?.Price?.Metadata?.GetValueOrDefault(StripeConstants.MetadataKeys.PurchasableReference);
            if (string.IsNullOrEmpty(reference))
            {
                continue;
            }

            // Match on DiscountId: line.DiscountAmounts[].Discount is unexpanded (null) on real responses.
            var matches = (line.DiscountAmounts ?? [])
                .Where(amount => !string.IsNullOrEmpty(amount.DiscountId)
                                 && resolved.TryGetValue(amount.DiscountId, out var discount) && discount.IsItemScoped)
                .ToArray();
            if (matches.Length == 0)
            {
                continue;
            }

            var lineDiscounts = matches.Select(amount => resolved[amount.DiscountId].ToPreviewDiscount(amount.Amount / 100m)).ToArray();
            foreach (var amount in matches)
            {
                attached.Add(amount.DiscountId);
            }
            itemLevel[reference] = itemLevel.TryGetValue(reference, out var existing) ? [.. existing, .. lineDiscounts] : lineDiscounts;
        }

        // An item-scoped coupon matching no referenced line vanishes from both sets — never silently.
        foreach (var (discountId, discount) in resolved)
        {
            if (discount.IsItemScoped && !attached.Contains(discountId))
            {
                logger.LogError("Item-scoped discount {DiscountId} ({Label}) matched no line; dropped.", discountId, discount.Coupon.Name);
            }
        }

        return new PartitionedDiscounts(cartLevel, itemLevel);
    }

    private readonly record struct ResolvedDiscount(Coupon Coupon, decimal AggregateAmount)
    {
        internal bool IsItemScoped => Coupon.AppliesTo?.Products?.Count > 0;

        internal InvoicePreviewDiscount ToPreviewDiscount(decimal amount) => new()
        {
            Type = Coupon.PercentOff is not null ? BitwardenDiscountType.PercentOff : BitwardenDiscountType.AmountOff,
            Value = Coupon.PercentOff ?? (Coupon.AmountOff ?? 0) / 100m,
            Amount = amount,
            Label = Coupon.Name,
        };
    }
}
