using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Invoicing.InvoicePreviews.Models;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Invoicing.InvoicePreviews;

/// <summary>
/// Invoice discounts split by scope: <see cref="CartLevel"/> applies to the whole invoice,
/// while <see cref="ItemLevel"/> holds per-line discounts keyed by purchasable reference.
/// </summary>
internal sealed record PartitionedDiscounts(
    InvoicePreviewDiscount[] CartLevel,
    IReadOnlyDictionary<string, InvoicePreviewDiscount[]> ItemLevel);

/// <summary>
/// Splits invoice discounts into cart-level and item-level buckets. Coupons are resolved
/// once and matched onto the lines they apply to.
/// </summary>
internal static class DiscountMapper
{
    internal static PartitionedDiscounts Partition(Invoice invoice, ILogger logger)
    {
        var resolved = ResolveInvoiceDiscounts(invoice, logger);

        var cartLevel = resolved.Values
            .Where(discount => !discount.IsItemScoped)
            .Select(discount => discount.ToPreviewDiscount(discount.AggregateAmount))
            .ToArray();

        var itemLevel = new Dictionary<string, List<InvoicePreviewDiscount>>();
        var attached = new HashSet<string>();
        foreach (var line in invoice.Lines?.Data ?? [])
        {
            // Prorations are discountable=false: the discount rides in the line amount, not discount_amounts.
            if (line.Parent?.SubscriptionItemDetails?.Proration == true)
            {
                continue;
            }

            var reference = line.Pricing?.PriceDetails?.Price?.Metadata?.GetValueOrDefault(StripeConstants.MetadataKeys.PurchasableReference);
            if (!PurchasableReferences.IsKnown(reference))
            {
                continue;
            }

            // Match on DiscountId: line.DiscountAmounts[].Discount is unexpanded (null) on real responses.
            foreach (var amount in line.DiscountAmounts ?? [])
            {
                if (string.IsNullOrEmpty(amount.DiscountId)
                    || !resolved.TryGetValue(amount.DiscountId, out var discount)
                    || !discount.IsItemScoped)
                {
                    continue;
                }

                attached.Add(amount.DiscountId);
                itemLevel.TryAdd(reference, []);
                itemLevel[reference].Add(discount.ToPreviewDiscount(amount.Amount / 100m));
            }
        }

        foreach (var (discountId, discount) in resolved)
        {
            if (discount.IsItemScoped && !attached.Contains(discountId))
            {
                logger.LogError("Item-scoped discount {DiscountId} ({Label}) matched no line; dropped.", discountId, discount.Coupon.Name);
            }
        }

        return new PartitionedDiscounts(
            cartLevel,
            itemLevel.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray()));
    }

    private static Dictionary<string, ResolvedDiscount> ResolveInvoiceDiscounts(Invoice invoice, ILogger logger)
    {
        var resolved = new Dictionary<string, ResolvedDiscount>();
        foreach (var total in invoice.TotalDiscountAmounts ?? [])
        {
            if (string.IsNullOrEmpty(total.DiscountId))
            {
                logger.LogError("Discount amount ({Amount}) has no discount id; dropped.", total.Amount);
                continue;
            }
            if (total.Discount?.Source?.Coupon is not { } coupon)
            {
                logger.LogError("Discount {DiscountId} has no expanded coupon; dropped.", total.DiscountId);
                continue;
            }
            resolved[total.DiscountId] = new ResolvedDiscount(coupon, total.Amount / 100m);
        }
        return resolved;
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
